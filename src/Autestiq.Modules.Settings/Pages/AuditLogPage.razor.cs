using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Autestiq.Contracts.Common;
using Autestiq.Contracts.Identity;
using Autestiq.Modules.Settings.Resources;

namespace Autestiq.Modules.Settings.Pages;

/// <summary>The organization's audit log, filtered by person, action and day (E1.2).</summary>
public sealed partial class AuditLogPage : ComponentBase, IDisposable
{
    /// <summary>
    /// One read at a time. The circuit's database context takes one operation at a time, and two filters changed in
    /// quick succession would otherwise reach it together ("A second operation was started on this context
    /// instance"). A read that waited asks with the filters as they are when its turn comes.
    /// </summary>
    private readonly SemaphoreSlim _turn = new(1, 1);

    /// <summary>Entries per page.</summary>
    private const int PageSize = 50;

    [Inject]
    private IAuditLog Log { get; set; } = null!;

    [Inject]
    private ICurrentUser CurrentUser { get; set; } = null!;

    [Inject]
    private IStringLocalizer<SettingsResources> Localizer { get; set; } = null!;

    private bool Loading { get; set; } = true;

    private string? LoadError { get; set; }

    /// <summary>Prerendered, the page answers nothing; its filters stay disabled until they would work.</summary>
    private bool Ready => RendererInfo.IsInteractive;

    private PagedResult<AuditRecord> Entries { get; set; } = PagedResult.Empty<AuditRecord>();

    private AuditFilterChoices Choices { get; set; } = AuditFilterChoices.None;

    /// <summary>The page shown, counting from zero, as <see cref="Autestiq.UI.Shared.Components.TPagination"/> does.</summary>
    private int PageIndex { get; set; }

    private string? Actor { get; set; }

    private string? Action { get; set; }

    private DateOnly? From { get; set; }

    private DateOnly? To { get; set; }

    private bool Filtered => Actor is not null || Action is not null || From is not null || To is not null;

    /// <summary>A range that ends before it starts finds nothing; the page says why instead of showing an empty log.</summary>
    private bool DateProblem => From is { } from && To is { } to && to < from;

    private bool CanRead => CurrentUser.Can(Permission.ReadAuditLog);

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        if (!CanRead)
        {
            return;
        }

        await LoadAsync().ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        Loading = true;
        LoadError = null;

        await _turn.WaitAsync().ConfigureAwait(false);

        try
        {
            Choices = await Log.ReadFilterChoicesAsync(CancellationToken.None).ConfigureAwait(false);
            Entries = DateProblem
                ? PagedResult.Empty<AuditRecord>()
                : await Log.ReadAsync(new AuditQuery(PageIndex + 1, PageSize, Actor, Action, From, To), CancellationToken.None).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            LoadError = Localizer["Audit_LoadFailed"];
        }
        finally
        {
            Loading = false;
            _turn.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _turn.Dispose();

    private Task ChooseActorAsync(string? value)
    {
        Actor = Blank(value);
        return FromFirstPageAsync();
    }

    private Task ChooseActionAsync(string? value)
    {
        Action = Blank(value);
        return FromFirstPageAsync();
    }

    private Task ChooseFromAsync(string? value)
    {
        From = Day(value);
        return FromFirstPageAsync();
    }

    private Task ChooseToAsync(string? value)
    {
        To = Day(value);
        return FromFirstPageAsync();
    }

    private Task ClearAsync()
    {
        Actor = null;
        Action = null;
        From = null;
        To = null;
        return FromFirstPageAsync();
    }

    private Task GoToAsync(int index)
    {
        PageIndex = index;
        return LoadAsync();
    }

    private Task FromFirstPageAsync()
    {
        PageIndex = 0;
        return LoadAsync();
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>A date field's value, which the browser always sends as yyyy-MM-dd, whatever the person's language.</summary>
    private static DateOnly? Day(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;

    private static string? Text(DateOnly? day) => day?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
