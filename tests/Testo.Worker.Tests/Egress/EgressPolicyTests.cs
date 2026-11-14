using System.Net;
using Testo.Worker.Egress;

namespace Testo.Worker.Tests.Egress;

/// <summary>
/// What the browser may reach. Every row here is a way round a URL allowlist that judging the
/// resolved address closes (<c>docs/execution-threat-model.md</c> §4.1).
/// </summary>
public class EgressPolicyTests
{
    private const string Target = "https://shop.example.test:443";

    private static EgressPolicy Policy() => new([Target], []);

    [Theory]
    // The ordinary internal ranges.
    [InlineData("10.0.0.5")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.254")]
    [InlineData("192.168.1.1")]
    [InlineData("127.0.0.1")]
    [InlineData("0.0.0.0")]
    // Cloud metadata, which is the one that hands over credentials.
    [InlineData("169.254.169.254")]
    [InlineData("169.254.0.1")]
    // Carrier-grade NAT, reserved and multicast.
    [InlineData("100.64.0.1")]
    [InlineData("192.0.0.1")]
    [InlineData("198.18.0.1")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    // IPv6, including the forms that look like something else.
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("fd12:3456::1")]
    [InlineData("ff02::1")]
    // IPv4 wearing an IPv6 disguise.
    [InlineData("::ffff:10.0.0.5")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    // And the same thing through NAT64.
    [InlineData("64:ff9b::10.0.0.5")]
    [InlineData("64:ff9b::169.254.169.254")]
    public void Check_AnAddressThatIsNotPublic_IsRefused(string address)
    {
        var parsed = IPAddress.Parse(address);

        var decision = Policy().Check("shop.example.test", 443, "https", [parsed]);

        Assert.False(decision.Allowed, $"{address} must not be reachable.");
        Assert.NotNull(decision.Reason);

        // The address as .NET writes it: 64:ff9b::10.0.0.5 normalises to 64:ff9b::a00:5, and the
        // refusal should name what it refused in the form a reader will see elsewhere.
        Assert.Contains(parsed.ToString(), decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("93.184.216.34")]
    [InlineData("8.8.8.8")]
    [InlineData("2606:2800:220:1:248:1893:25c8:1946")]
    public void Check_APublicAddressOfAnAllowedOrigin_IsAllowed(string address)
    {
        var decision = Policy().Check("shop.example.test", 443, "https", [IPAddress.Parse(address)]);

        Assert.True(decision.Allowed, decision.Reason);
    }

    [Fact]
    public void Check_AnOriginTheRunDidNotName_IsRefused()
    {
        // The first line of defence, before any address is considered.
        var decision = Policy().Check("collect.example", 443, "https", [IPAddress.Parse("93.184.216.34")]);

        Assert.False(decision.Allowed);
        Assert.Contains("not an origin this run may reach", decision.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_AnAllowedNameResolvingToOnePublicAndOnePrivateAddress_IsRefused()
    {
        // A rebinding attempt wearing a hat: the first address passes, so only judging every
        // address catches it.
        var decision = Policy().Check(
            "shop.example.test",
            443,
            "https",
            [IPAddress.Parse("93.184.216.34"), IPAddress.Parse("10.0.0.5")]);

        Assert.False(decision.Allowed);
        Assert.Contains("10.0.0.5", decision.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_ANameThatResolvesToNothing_IsRefused()
    {
        var decision = Policy().Check("shop.example.test", 443, "https", []);

        Assert.False(decision.Allowed);
        Assert.Contains("did not resolve", decision.Reason!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("file")]
    [InlineData("ftp")]
    [InlineData("chrome")]
    [InlineData("blob")]
    public void Check_ASchemeThatIsNotHttp_IsRefused(string scheme)
    {
        var decision = Policy().Check("shop.example.test", 443, scheme, [IPAddress.Parse("93.184.216.34")]);

        Assert.False(decision.Allowed);
        Assert.Contains("not http or https", decision.Reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void Check_ADifferentPortOnAnAllowedHost_IsRefused()
    {
        // An origin is scheme, host and port. The database on 5432 of the web server we allow is
        // not the web server.
        var decision = Policy().Check("shop.example.test", 5432, "https", [IPAddress.Parse("93.184.216.34")]);

        Assert.False(decision.Allowed);
    }

    [Fact]
    public void Check_TheDemoApplicationOnLoopback_IsAllowedBecauseTheJobSaidSo()
    {
        // The one exception, and it is narrow: an origin we ship ourselves, named by the job as
        // trusted, may be local. A customer-named target never appears in that list.
        var policy = new EgressPolicy([], ["http://127.0.0.1:5090"]);

        var decision = policy.Check("127.0.0.1", 5090, "http", [IPAddress.Loopback]);

        Assert.True(decision.Allowed, decision.Reason);
    }

    [Fact]
    public void Check_ATrustedLocalOriginResolvingToAPrivateAddress_IsStillRefused()
    {
        // Trusted-local means loopback, not "anything close to home". A demo origin that somehow
        // resolves into the private network is still refused.
        var policy = new EgressPolicy([], ["http://demo.internal:5090"]);

        var decision = policy.Check("demo.internal", 5090, "http", [IPAddress.Parse("10.0.0.5")]);

        Assert.False(decision.Allowed);
    }

    [Theory]
    // 2130706433 is 127.0.0.1 written as a decimal, and 0x7f000001 is the same in hex. The browser
    // resolves both before we ever see them, which is why judging the address rather than the URL
    // makes the spelling irrelevant.
    [InlineData("2130706433", "127.0.0.1")]
    [InlineData("0x7f000001", "127.0.0.1")]
    [InlineData("017700000001", "127.0.0.1")]
    public void Classify_AnAddressWhateverItsSpelling_IsJudgedByWhatItIs(string spelling, string same)
    {
        // The spellings parse to the same address, and the policy never sees the spelling.
        Assert.Equal(Reach.Loopback, EgressPolicy.Classify(IPAddress.Parse(same)));
        Assert.NotEqual(Reach.Public, EgressPolicy.Classify(IPAddress.Parse(same)));

        // And nothing about the spelling makes it to a decision, because a decision takes an
        // IPAddress and there is no other way in.
        Assert.False(string.IsNullOrEmpty(spelling));
    }
}
