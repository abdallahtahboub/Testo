namespace Testo.Worker.Egress;

/// <summary>Whether a connection may be made, and why not when it may not.</summary>
/// <param name="Allowed">Whether to connect.</param>
/// <param name="Reason">Why it was refused, in words a person reading a run can use.</param>
public readonly record struct EgressDecision(bool Allowed, string? Reason)
{
    /// <summary>Connect.</summary>
    public static EgressDecision Allow { get; } = new(true, null);

    /// <summary>Do not connect, for this reason.</summary>
    public static EgressDecision Refuse(string reason) => new(false, reason);
}
