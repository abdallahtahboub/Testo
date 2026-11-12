using Microsoft.Extensions.Options;

namespace Testo.Mocks;

/// <summary>
/// Hands out deterministic random number generators for mock data.
/// </summary>
/// <remarks>
/// Every data area asks for its own named stream ("projects", "defects", …). A stream depends
/// only on the configured seed and its name, so adding data to one area never changes the data
/// of another, and the result is the same in every process and on every machine.
/// </remarks>
public sealed class MockRandomSource(IOptions<MockOptions> options)
{
    private readonly int _seed = options.Value.Seed;

    /// <summary>Creates the generator for the given stream.</summary>
    public Random Create(string stream)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stream);
        return new Random(StableHash(_seed, stream));
    }

    /// <summary>
    /// FNV-1a over the seed and the stream name. <see cref="string.GetHashCode()"/> is not used
    /// because it is randomized per process.
    /// </summary>
    internal static int StableHash(int seed, string stream)
    {
        unchecked
        {
            const uint offsetBasis = 2166136261;
            const uint prime = 16777619;

            var hash = offsetBasis;
            hash = (hash ^ (uint)seed) * prime;
            foreach (var character in stream)
            {
                hash = (hash ^ character) * prime;
            }

            return (int)hash;
        }
    }
}
