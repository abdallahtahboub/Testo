using System.Net;
using System.Net.Sockets;

namespace Testo.Worker.Egress;

/// <summary>
/// Decides whether the browser may connect somewhere.
/// </summary>
/// <remarks>
/// <para>
/// The rule that matters: this judges <strong>resolved addresses</strong>, not URLs. A name is
/// checked by connecting to the address it resolved to and asking about that address, which is
/// what makes DNS rebinding, redirect chains, in-page <c>fetch</c>, and addresses written in
/// decimal or as IPv4-mapped IPv6 all the same question with the same answer
/// (<c>docs/execution-threat-model.md</c> §4.1).
/// </para>
/// <para>
/// Default deny. An address is refused unless it is a public one belonging to an origin the run
/// named, or it belongs to an origin we ship ourselves and the job said so.
/// </para>
/// </remarks>
public sealed class EgressPolicy(IReadOnlyList<string> allowedOrigins, IReadOnlyList<string> trustedLocalOrigins)
{
    private readonly HashSet<string> _allowed = Normalize(allowedOrigins);
    private readonly HashSet<string> _trustedLocal = Normalize(trustedLocalOrigins);

    /// <summary>Whether the browser may open a connection to this host and port.</summary>
    /// <param name="host">The host as the browser asked for it: a name or an address.</param>
    /// <param name="port">The port.</param>
    /// <param name="scheme">The scheme, which must be http or https.</param>
    /// <param name="addresses">What the host resolved to. Every one of them is judged.</param>
    public EgressDecision Check(string host, int port, string scheme, IReadOnlyList<IPAddress> addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);

        if (!scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
            && !scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            return EgressDecision.Refuse($"the scheme '{scheme}' is not http or https");
        }

        var origin = $"{scheme.ToLowerInvariant()}://{host.ToLowerInvariant()}:{port}";
        var isAllowedOrigin = _allowed.Contains(origin);
        var isTrustedLocal = _trustedLocal.Contains(origin);

        if (!isAllowedOrigin && !isTrustedLocal)
        {
            return EgressDecision.Refuse($"{origin} is not an origin this run may reach");
        }

        if (addresses.Count == 0)
        {
            return EgressDecision.Refuse($"{host} did not resolve to any address");
        }

        // Every address, not the first. A name that resolves to one public and one private address
        // is a rebinding attempt wearing a hat.
        foreach (var address in addresses)
        {
            var reach = Classify(address);

            if (reach == Reach.Public)
            {
                continue;
            }

            // Only an origin we ship ourselves may be local, and only because the job said so.
            if (isTrustedLocal && reach == Reach.Loopback)
            {
                continue;
            }

            return EgressDecision.Refuse($"{host} resolves to {address}, which is {Describe(reach)}");
        }

        return EgressDecision.Allow;
    }

    /// <summary>Whether an address is one the internet can reach, or something closer to home.</summary>
    public static Reach Classify(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        // ::ffff:10.0.0.5 is 10.0.0.5 wearing a disguise, and so is ::ffff:7f00:1.
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => ClassifyV4(address.GetAddressBytes()),
            AddressFamily.InterNetworkV6 => ClassifyV6(address),
            _ => Reach.Refused,
        };
    }

    private static Reach ClassifyV4(byte[] b) => (b[0], b[1]) switch
    {
        (0, _) => Reach.Unspecified,                              // 0.0.0.0/8
        (10, _) => Reach.Private,                                 // 10.0.0.0/8
        (127, _) => Reach.Loopback,                               // 127.0.0.0/8
        (169, 254) => Reach.LinkLocal,                            // 169.254.0.0/16, metadata lives here
        (172, >= 16 and <= 31) => Reach.Private,                  // 172.16.0.0/12
        (192, 168) => Reach.Private,                              // 192.168.0.0/16
        (100, >= 64 and <= 127) => Reach.CarrierGrade,            // 100.64.0.0/10
        (192, 0) when b[2] == 0 => Reach.Reserved,                // 192.0.0.0/24
        (198, >= 18 and <= 19) => Reach.Reserved,                 // 198.18.0.0/15, benchmarking
        (>= 224 and <= 239, _) => Reach.Multicast,                // 224.0.0.0/4
        (>= 240, _) => Reach.Reserved,                            // 240.0.0.0/4 and broadcast
        _ => Reach.Public,
    };

    private static Reach ClassifyV6(IPAddress address)
    {
        if (IPAddress.IPv6Loopback.Equals(address))
        {
            return Reach.Loopback;
        }

        if (IPAddress.IPv6Any.Equals(address))
        {
            return Reach.Unspecified;
        }

        if (address.IsIPv6LinkLocal)
        {
            return Reach.LinkLocal;
        }

        if (address.IsIPv6Multicast)
        {
            return Reach.Multicast;
        }

        var b = address.GetAddressBytes();

        // fc00::/7, the unique local addresses: another organization's internal network.
        if ((b[0] & 0xFE) == 0xFC)
        {
            return Reach.Private;
        }

        // 64:ff9b::/96 maps IPv4 into IPv6 for NAT64. Judge the IPv4 address inside it.
        if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xFF && b[3] == 0x9B)
        {
            return ClassifyV4([b[12], b[13], b[14], b[15]]);
        }

        return Reach.Public;
    }

    private static string Describe(Reach reach) => reach switch
    {
        Reach.Loopback => "this machine",
        Reach.Private => "a private network",
        Reach.LinkLocal => "link-local, where cloud metadata lives",
        Reach.CarrierGrade => "carrier-grade NAT space",
        Reach.Multicast => "a multicast address",
        Reach.Unspecified => "the unspecified address",
        Reach.Reserved => "a reserved range",
        _ => "not a public address",
    };

    private static HashSet<string> Normalize(IReadOnlyList<string> origins)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);

        foreach (var origin in origins ?? [])
        {
            if (Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                set.Add($"{uri.Scheme.ToLowerInvariant()}://{uri.Host.ToLowerInvariant()}:{uri.Port}");
            }
        }

        return set;
    }
}

/// <summary>How far away an address is.</summary>
public enum Reach
{
    /// <summary>Somewhere on the internet.</summary>
    Public = 0,

    /// <summary>This machine.</summary>
    Loopback = 1,

    /// <summary>A private network: ours, or the customer's.</summary>
    Private = 2,

    /// <summary>Link-local, which is where cloud metadata services live.</summary>
    LinkLocal = 3,

    /// <summary>Carrier-grade NAT space.</summary>
    CarrierGrade = 4,

    /// <summary>A multicast address.</summary>
    Multicast = 5,

    /// <summary>The unspecified address.</summary>
    Unspecified = 6,

    /// <summary>Reserved by a standard for something that is not ordinary traffic.</summary>
    Reserved = 7,

    /// <summary>An address family we do not speak.</summary>
    Refused = 8,
}
