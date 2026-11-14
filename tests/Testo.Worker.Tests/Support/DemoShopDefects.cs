using Testo.DemoShop;

namespace Testo.Worker.Tests.Support;

/// <summary>
/// Switches the shop's defects on and off for one test.
/// </summary>
/// <remarks>
/// The shop is in this process, so this sets them directly rather than over HTTP. The state is
/// process-wide, which is why every test that touches it belongs to <see cref="Serially"/>: two
/// tests disagreeing about whether the checkout is broken would be a flake we wrote ourselves.
/// </remarks>
internal sealed class DemoShopDefects : IDisposable
{
    private DemoShopDefects()
    {
    }

    /// <summary>Switches exactly these on, and everything else off, until the test ends.</summary>
    public static DemoShopDefects On(params string[] defects)
    {
        if (!Defects.Set(defects))
        {
            throw new ArgumentException($"Unknown defect in: {string.Join(", ", defects)}", nameof(defects));
        }

        return new DemoShopDefects();
    }

    /// <summary>Puts the shop back in working order.</summary>
    public void Dispose() => Defects.Set([]);
}

/// <summary>
/// The collection for tests that switch the shop's defects. One at a time, because the switch is
/// process-wide.
/// </summary>
[CollectionDefinition(Name)]
public sealed class Serially
{
    /// <summary>What to put in <c>[Collection]</c>.</summary>
    public const string Name = "the demo shop's defects";
}
