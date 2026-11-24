using Autestiq.Domain.Common;

namespace Autestiq.Domain.Testing;

/// <summary>
/// One request for a plan, recorded <em>before</em> the model is asked (Milestone 5.1 R1, R-17). Append-only: what
/// happened to it is a separate <see cref="PlanningAttemptOutcome"/>, written once. An attempt without an outcome may
/// have been billed, so the budgets count it at its <see cref="CeilingUsd"/> until it has one.
/// </summary>
public sealed class PlanningAttempt : Entity, IBelongsToOrganization, IAppendOnly
{
    /// <inheritdoc />
    public Guid OrganizationId { get; set; }

    /// <summary>The test it plans.</summary>
    public Guid TestCaseId { get; set; }

    /// <summary>The model asked.</summary>
    public required string Model { get; set; }

    /// <summary>The most this attempt may spend, in US dollars: what it counts for until its outcome is known.</summary>
    public decimal CeilingUsd { get; set; }
}

/// <summary>How a <see cref="PlanningAttempt"/> ended, written once. Append-only.</summary>
public sealed class PlanningAttemptOutcome : Entity, IBelongsToOrganization, IAppendOnly
{
    /// <inheritdoc />
    public Guid OrganizationId { get; set; }

    /// <summary>The attempt it ends. Unique: an attempt ends once.</summary>
    public Guid AttemptId { get; set; }

    /// <summary>Whether the model answered, or the attempt was found unfinished after its lease.</summary>
    public PlanningAttemptEnd End { get; set; }

    /// <summary>
    /// What it cost, in US dollars: the provider's reckoning when it answered; the ceiling when interrupted, because
    /// nobody knows whether the provider billed it.
    /// </summary>
    public decimal CostUsd { get; set; }

    /// <summary>Tokens the provider reported; <c>null</c> when interrupted.</summary>
    public int? Tokens { get; set; }
}

/// <summary>How a planning attempt ended.</summary>
public enum PlanningAttemptEnd
{
    /// <summary>The model answered, with a plan or without one; its cost is known.</summary>
    Answered,

    /// <summary>Found unfinished after its lease: a crash or a restart. Its cost is unknown and taken at the ceiling.</summary>
    Interrupted,
}
