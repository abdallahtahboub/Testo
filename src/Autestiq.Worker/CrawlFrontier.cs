using System.Text.RegularExpressions;

namespace Autestiq.Worker;

/// <summary>
/// Decides which address a crawl opens next, and whether it has stopped short when it stops.
/// </summary>
/// <remarks>
/// <para>
/// An application with a page for every date, or every product, has far more addresses than kinds
/// of page. A crawl that takes addresses as they come spends its budget on one kind: the clinic's
/// first twelve pages were three dates of one service, and its booking form was never described.
/// </para>
/// <para>
/// So addresses are sorted by kind, where a kind is the address with its numbers masked.
/// One address of every kind comes first. Then a second address of each kind is opened as a check.
/// If it turns out to be the same kind of page as the first, the kind is settled and its other
/// addresses are left alone. If it turns out different, the numbers in that address meant more than
/// a date, and every address of that kind is opened like any other.
/// </para>
/// <para>
/// When the budget runs out, the crawl has stopped short only if something is left that might be a
/// kind of page nobody has seen: an address of a new kind, or one of a kind not yet settled. Twenty
/// more dates of a settled kind are not part of the application that is missing.
/// </para>
/// </remarks>
public sealed partial class CrawlFrontier
{
    private readonly Queue<string> _newKinds = new();
    private readonly Queue<string> _moreOfAKind = new();
    private readonly HashSet<string> _noted = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _kindsNoted = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _firstShape = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _settled = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _mixed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Starts with one address to open.</summary>
    public CrawlFrontier(string start) => Note(start);

    /// <summary>Whether anything is left that might be a kind of page not yet opened.</summary>
    public bool HasUnseen =>
        _newKinds.Count > 0 || _moreOfAKind.Any(path => !_settled.Contains(KindOf(path)));

    /// <summary>The kind an address belongs to: the address with every number masked.</summary>
    public static string KindOf(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return Digits().Replace(path, "#");
    }

    /// <summary>Takes note of an address the crawl has come across. An address already noted is ignored.</summary>
    public void Note(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!_noted.Add(path))
        {
            return;
        }

        // The first address of a kind is opened before a second address of any kind.
        if (_kindsNoted.Add(KindOf(path)))
        {
            _newKinds.Enqueue(path);
        }
        else
        {
            _moreOfAKind.Enqueue(path);
        }
    }

    /// <summary>The next address to open, or <c>null</c> when nothing is left that is worth opening.</summary>
    public string? Next()
    {
        if (_newKinds.Count > 0)
        {
            return _newKinds.Dequeue();
        }

        while (_moreOfAKind.Count > 0)
        {
            var path = _moreOfAKind.Dequeue();

            if (!_settled.Contains(KindOf(path)))
            {
                return path;
            }
        }

        return null;
    }

    /// <summary>
    /// Records what an opened address turned out to be.
    /// </summary>
    /// <param name="path">The address that was opened.</param>
    /// <param name="shape">What kind of page it showed, as the crawl tells kinds of page apart.</param>
    public void Saw(string path, string shape)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(shape);

        var kind = KindOf(path);

        if (!_firstShape.TryGetValue(kind, out var first))
        {
            _firstShape[kind] = shape;
            return;
        }

        if (_mixed.Contains(kind))
        {
            return;
        }

        if (string.Equals(first, shape, StringComparison.Ordinal))
        {
            _settled.Add(kind);
        }
        else
        {
            // The numbers in this address meant more than a date. From here on it is treated as any
            // other address, and it is never settled.
            _settled.Remove(kind);
            _mixed.Add(kind);
        }
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();
}
