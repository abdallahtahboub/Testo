using Autestiq.Contracts.Common;
using Autestiq.Contracts.Execution;

namespace Autestiq.Contracts.Projects;

/// <summary>A project, as a list shows it.</summary>
/// <param name="ProjectId">Its id.</param>
/// <param name="Name">Its name.</param>
/// <param name="Key">Its short key, upper case, as it appears in addresses: <c>/app/p/SHOP/…</c>.</param>
/// <param name="Description">What it is for, if anyone said.</param>
/// <param name="Environments">How many environments it has.</param>
/// <param name="AiProcessingAllowedAt">When AI planning was allowed to process its page content, or <c>null</c>: off by default.</param>
public sealed record ProjectSummary(Guid ProjectId, string Name, string Key, string? Description, int Environments, DateTimeOffset? AiProcessingAllowedAt = null, int EvidenceRetentionDays = EvidenceRetention.DefaultDays);

/// <summary>How long a project keeps its runs' screenshots (gap analysis G-5).</summary>
public static class EvidenceRetention
{
    /// <summary>What a project keeps unless someone chooses less: 90 days (<c>docs/domain-model.md</c> §7).</summary>
    public const int DefaultDays = 90;

    /// <summary>The choices, longest first; 0 keeps no screenshot at all. Nothing is kept longer than 90 days.</summary>
    public static IReadOnlyList<int> AllowedDays { get; } = [90, 30, 7, 0];
}

/// <summary>What kind of site an environment is. Production is refused for runs until an owner authorizes it.</summary>
public enum EnvironmentKind
{
    /// <summary>A test, staging or demo site.</summary>
    Test = 1,

    /// <summary>The live site real customers use.</summary>
    Production = 2,
}

/// <summary>An environment: one address of the application a project tests, with the proof that it may be tested.</summary>
/// <param name="EnvironmentId">Its id.</param>
/// <param name="Name">Its name, such as "Staging".</param>
/// <param name="Origin">Its origin, normalized: <c>https://staging.shop.example:443</c>.</param>
/// <param name="Proof">Where the organization's proof of control of that origin stands, or <c>null</c> when none was asked for.</param>
/// <param name="Kind">Test or production.</param>
/// <param name="ProductionAuthorizedAt">When an owner authorized testing it, for a production environment; <c>null</c> until then.</param>
public sealed record EnvironmentSummary(
    Guid EnvironmentId,
    string Name,
    string Origin,
    OriginProofState? Proof,
    EnvironmentKind Kind = EnvironmentKind.Test,
    DateTimeOffset? ProductionAuthorizedAt = null)
{
    /// <summary>Whether runs may be aimed at it, as far as its kind goes: always for a test site, after authorization for production.</summary>
    public bool KindAllowsRuns => Kind == EnvironmentKind.Test || ProductionAuthorizedAt is not null;
}

/// <summary>What is needed to create a project.</summary>
public sealed record NewProject(string Name, string Key, string? Description);

/// <summary>What is needed to add an environment. Test unless said otherwise: production is never the default.</summary>
public sealed record NewEnvironment(string Name, string Origin, EnvironmentKind Kind = EnvironmentKind.Test);

/// <summary>
/// Projects and their environments (Milestone 5). Everything is scoped to the current organization by the tenant
/// filter. Reading requires <see cref="Identity.Permission.Read"/>; every change requires
/// <see cref="Identity.Permission.ManageProjects"/>. Failures are resource keys.
/// </summary>
public interface IProjectService
{
    /// <summary>The organization's active projects, by name.</summary>
    Task<IReadOnlyList<ProjectSummary>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>One project by its key, in any case, or <c>null</c> when this organization has none with that key.</summary>
    Task<ProjectSummary?> FindAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Creates a project, within the number the organization's plan allows. Returns its key.</summary>
    Task<OperationResult<string>> CreateAsync(NewProject project, CancellationToken cancellationToken = default);

    /// <summary>
    /// Allows AI planning to process the project's discovered page content: titles, labels and visible text, sent to
    /// the configured model provider. Never credentials, screenshots or traces. Requires ManageProjects; audited.
    /// </summary>
    Task<OperationResult> AllowAiProcessingAsync(Guid projectId, bool acknowledged, CancellationToken cancellationToken = default);

    /// <summary>Withdraws that permission: no more planning for the project until it is allowed again. Audited.</summary>
    Task<OperationResult> WithdrawAiProcessingAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets how many days the project's screenshots are kept, one of <see cref="EvidenceRetention.AllowedDays"/>. Needs
    /// <see cref="Identity.Permission.ManageProjects"/>; audited. A shorter time applies to screenshots already kept.
    /// </summary>
    Task<OperationResult> SetEvidenceRetentionAsync(Guid projectId, int days, CancellationToken cancellationToken = default);

    /// <summary>A project's environments, by name, each with the state of its origin's proof.</summary>
    Task<IReadOnlyList<EnvironmentSummary>> ListEnvironmentsAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>Adds an environment to a project. Its origin must be a plain http or https origin.</summary>
    Task<OperationResult<Guid>> AddEnvironmentAsync(Guid projectId, NewEnvironment environment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Authorizes runs against a production environment. Owners only, and only with the acknowledgement that tests
    /// can change real data there. Audited.
    /// </summary>
    Task<OperationResult> AuthorizeProductionAsync(Guid environmentId, bool acknowledged, CancellationToken cancellationToken = default);

    /// <summary>Withdraws that authorization; runs are refused again. Owners only. Audited.</summary>
    Task<OperationResult> WithdrawProductionAsync(Guid environmentId, CancellationToken cancellationToken = default);

    /// <summary>Removes an environment. Its origin's proof stays, as it belongs to the organization, not the environment.</summary>
    Task<OperationResult> RemoveEnvironmentAsync(Guid environmentId, CancellationToken cancellationToken = default);
}

/// <summary>The rules for a project's key, in one place for the service and the form.</summary>
public static class ProjectKeys
{
    /// <summary>The shortest key.</summary>
    public const int MinLength = 2;

    /// <summary>The longest key; the database column holds this many.</summary>
    public const int MaxLength = 10;

    /// <summary>The key as it is stored, upper case, or <c>null</c> when it is not one: a letter, then letters or digits.</summary>
    public static string? Normalize(string? key)
    {
        var trimmed = key?.Trim().ToUpperInvariant();

        return trimmed is { Length: >= MinLength and <= MaxLength }
            && char.IsAsciiLetterUpper(trimmed[0])
            && trimmed.All(character => char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character))
            ? trimmed
            : null;
    }
}
