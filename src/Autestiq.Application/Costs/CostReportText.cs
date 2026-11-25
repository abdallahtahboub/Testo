using System.Globalization;
using System.Text;

namespace Autestiq.Application.Costs;

/// <summary>A cost report as text for an operator's terminal: a table, and what its figures rest on.</summary>
public static class CostReportText
{
    /// <summary>Writes the month's lines; with a rate, the costs are also shown in euros.</summary>
    public static string Write(string month, IReadOnlyList<CostLine> lines, decimal? eurosPerDollar)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var text = new StringBuilder();
        var invariant = CultureInfo.InvariantCulture;
        var total = lines[^1];

        text.Append(invariant, $"Cost report for {month}, in {total.Currency}").AppendLine(eurosPerDollar is { } shown ? string.Create(invariant, $" (and EUR at {shown} per dollar, for comparison only)") : "");
        text.AppendLine();
        text.AppendLine(invariant, $"{"Organization",-24} {"Runs",6} {"Billable",9} {"Minutes",8} {"Cost",12} {"Per run",10} {"Per minute",11} {"Our failures",13}");

        foreach (var line in lines)
        {
            text.Append(invariant, $"{line.Organization ?? "TOTAL",-24} {line.Runs,6} {line.BillableRuns,9} {line.BillableMinutes,8} ")
                .Append(invariant, $"{Amount(line.Cost, eurosPerDollar),12} {Amount(line.CostPerRun, eurosPerDollar),10} {Amount(line.CostPerBillableMinute, eurosPerDollar),11} ")
                .AppendLine(line.OurFailuresShare is { } share ? share.ToString("P1", invariant) : "-");
        }

        text.AppendLine();

        if (lines.Any(line => line.FromEstimates))
        {
            text.AppendLine("ESTIMATES: part of this cost is priced with estimated machine or storage prices, not prices anyone charges us.");
            text.AppendLine("Publish real ones with set-prices once hosting is chosen.");
        }

        text.AppendLine("Storage is priced for one month of keeping each run's evidence, until retention decides how long it is kept.");
        text.AppendLine("Model tokens cost nothing here yet: the product does not call a model.");
        return text.ToString();

        static string Amount(decimal? dollars, decimal? rate) => dollars switch
        {
            null => "-",
            { } value when rate is { } euros => string.Create(CultureInfo.InvariantCulture, $"{value:0.0000}/€{value * euros:0.0000}"),
            { } value => value.ToString("0.0000", CultureInfo.InvariantCulture),
        };
    }
}
