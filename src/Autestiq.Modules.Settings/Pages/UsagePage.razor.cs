using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Autestiq.Contracts.Usage;
using Autestiq.Modules.Settings.Resources;

namespace Autestiq.Modules.Settings.Pages;

/// <summary>The organization's usage this month against its plan (E1.3).</summary>
public sealed partial class UsagePage : ComponentBase
{
    /// <summary>From this share of the allowance on, the page says the month is nearly used up.</summary>
    private const double Nearly = 0.8;

    [Inject]
    private IUsageService UsageService { get; set; } = null!;

    [Inject]
    private IStringLocalizer<SettingsResources> Localizer { get; set; } = null!;

    private bool Loading { get; set; } = true;

    private string? LoadError { get; set; }

    /// <summary>The month's usage; <c>null</c> when the service gives none, which it does without permission.</summary>
    private OrganizationUsage? Usage { get; set; }

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        Loading = true;
        LoadError = null;

        try
        {
            Usage = await UsageService.GetCurrentMonthAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            LoadError = Localizer["Usage_LoadFailed"];
        }
        finally
        {
            Loading = false;
        }
    }

    /// <summary>The service's rule (ADR-0027): a run is refused once the month's minutes are all used.</summary>
    private static bool UsedUp(OrganizationUsage usage) => usage.BrowserMinutesUsed >= usage.BrowserMinutesAllowed;

    private static bool NearlyUsedUp(OrganizationUsage usage) =>
        !UsedUp(usage) && usage.BrowserMinutesUsed >= usage.BrowserMinutesAllowed * Nearly;

    private string State(OrganizationUsage usage)
    {
        if (UsedUp(usage))
        {
            return Localizer["Usage_State_UsedUp", ResetsOn(usage)];
        }

        var left = (usage.BrowserMinutesAllowed - usage.BrowserMinutesUsed).ToString("N0", CultureInfo.CurrentCulture);
        return NearlyUsedUp(usage) ? Localizer["Usage_State_Nearly", left] : Localizer["Usage_State_Left", left];
    }

    private static string StateClass(OrganizationUsage usage) =>
        UsedUp(usage) ? "usage__state usage__state--used-up" : NearlyUsedUp(usage) ? "usage__state usage__state--nearly" : "usage__state";

    /// <summary>The month the ledger counts, which it names as yyyy-MM in UTC.</summary>
    private static DateOnly MonthOf(OrganizationUsage usage) =>
        DateOnly.TryParseExact(usage.Month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var first)
            ? first
            : DateOnly.MinValue;

    private static string MonthName(OrganizationUsage usage) => MonthOf(usage).ToString("MMMM yyyy", CultureInfo.CurrentCulture);

    private static string ResetsOn(OrganizationUsage usage) => MonthOf(usage).AddMonths(1).ToString("D", CultureInfo.CurrentCulture);
}
