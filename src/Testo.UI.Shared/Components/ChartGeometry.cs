using System.Globalization;

namespace Testo.UI.Shared.Components;

/// <summary>
/// The shapes a chart is drawn from, following the mark specifications of the design
/// system: bars rounded at the data end and square at the baseline, lines 2px with round
/// joins, markers at least 8px across.
/// </summary>
internal static class ChartGeometry
{
    /// <summary>The corner radius of a bar's data end, in user units.</summary>
    public const double BarRadius = 4;

    private static string N(double value) => Math.Round(value, 2).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// A horizontal bar growing right from <paramref name="x"/>: rounded on the right,
    /// square where it meets the baseline. A bar shorter than the radius stays square,
    /// because rounding it would make it look longer than it is.
    /// </summary>
    public static string HorizontalBar(double x, double y, double width, double height)
    {
        if (width <= 0)
        {
            return "";
        }

        var radius = Math.Min(BarRadius, Math.Min(width, height / 2));
        var right = x + width;
        var bottom = y + height;

        return $"M{N(x)} {N(y)} H{N(right - radius)} " +
               $"A{N(radius)} {N(radius)} 0 0 1 {N(right)} {N(y + radius)} " +
               $"V{N(bottom - radius)} " +
               $"A{N(radius)} {N(radius)} 0 0 1 {N(right - radius)} {N(bottom)} " +
               $"H{N(x)} Z";
    }

    /// <summary>A polyline through the given points.</summary>
    public static string Polyline(IEnumerable<(double X, double Y)> points) =>
        string.Join(' ', points.Select(point => $"{N(point.X)},{N(point.Y)}"));

    /// <summary>Lays values out across a width, evenly spaced, scaled to a height.</summary>
    public static (double X, double Y)[] Plot(IReadOnlyList<double> values, double width, double height, double padding)
    {
        if (values.Count < 2)
        {
            return [];
        }

        var low = Math.Min(0, values.Min());
        var high = values.Max();
        var span = high - low;
        var stepX = width / (values.Count - 1);

        return [.. values.Select((value, index) => (
            X: index * stepX,
            Y: span == 0 ? height / 2 : height - padding - ((value - low) / span * (height - (2 * padding)))))];
    }
}
