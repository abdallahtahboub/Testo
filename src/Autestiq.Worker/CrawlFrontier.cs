using System.Text.RegularExpressions;

namespace Autestiq.Worker;

/// <summary>
/// Decides which address a crawl opens next, and whether it has stopped short when it stops.
/// </summary>
/// <remarks>
/// <para>
/// An application with a page for every date, or every invoice, has far more addresses than kinds of
/// page. A crawl that takes addresses as they come spends its budget on one kind: the clinic's first
/// twelve pages were three dates of one service, and its booking form was never described.
/// </para>
/// <para>
/// So addresses are sorted by kind, where a kind is the address with its numbers masked, and one address
/// of every kind comes first. Then each kind is sampled: its last address, then the one in the middle of
/// those left. A kind whose three samples are the same kind of page is settled, and its other addresses
/// are left alone. A kind whose samples differ is opened in full, in order.
/// </para>
/// <para>
/// The samples are spread on purpose. The invoice desk's first two invoices are both paid, and a crawl
/// that settled after two neighbours agreed concluded that no invoice has a "mark as paid" button.
/// Neighbours share more than distant addresses do: the same day, the same customer, the same status.
/// </para>
/// <para>
/// When the crawl stops, it has stopped short only if something is left that might be a kind of page
/// nobody has seen: an address of a new kind, or one of a kind not yet settled.
/// </para>
/// </remarks>
public sealed partial class CrawlFrontier
{
    /// <summary>How many samples of a kind must agree before the rest of it is left alone.</summary>
    public const int SamplesToSettle = 3;

    private readonly Queue<string> _newKinds = new();
    private readonly List<string> _kindOrder = [];
    private readonly Dictionary<string, List<string>> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _noted = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _firstShape = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _agreeing = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _settled = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _mixed = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Starts with one address to open.</summary>
    public CrawlFrontier(string start) => Note(start);

    /// <summary>Whether anything is left that might be a kind of page not yet opened.</summary>
    public bool HasUnseen =>
        _newKinds.Count > 0 || _kindOrder.Any(kind => !_settled.Contains(kind) && _pending[kind].Count > 0);

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

        var kind = KindOf(path);

        if (_pending.TryGetValue(kind, out var pending))
        {
            pending.Add(path);
            return;
        }

        // The first address of a kind is opened before a second address of any kind.
        _pending[kind] = [];
        _kindOrder.Add(kind);
        _newKinds.Enqueue(path);
    }

    /// <summary>The next address to open, or <c>null</c> when nothing is left that is worth opening.</summary>
    public string? Next()
    {
        if (_newKinds.Count > 0)
        {
            return _newKinds.Dequeue();
        }

        foreach (var kind in _kindOrder)
        {
            var pending = _pending[kind];

            if (_settled.Contains(kind) || pending.Count == 0)
            {
                continue;
            }

            int index;

            if (_mixed.Contains(kind) || !_firstShape.ContainsKey(kind))
            {
                // A kind known to vary is opened in order; so is one whose first page never loaded,
                // whose next address stands in as the first sample.
                index = 0;
            }
            else
            {
                // The last address, then the middle of those left: as far from the first as the list allows.
                index = _agreeing[kind] == 1 ? pending.Count - 1 : pending.Count / 2;
            }

            var path = pending[index];
            pending.RemoveAt(index);

            return path;
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
            _agreeing[kind] = 1;
            return;
        }

        if (_mixed.Contains(kind))
        {
            return;
        }

        if (string.Equals(first, shape, StringComparison.Ordinal))
        {
            if (++_agreeing[kind] >= SamplesToSettle)
            {
                _settled.Add(kind);
            }
        }
        else
        {
            // The numbers in this address meant more than a date. From here on it is opened like any
            // other address, and it is never settled.
            _mixed.Add(kind);
        }
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();
}
