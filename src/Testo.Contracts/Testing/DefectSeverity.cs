namespace Testo.Contracts.Testing;

/// <summary>How badly a defect affects the product. Severity is a fact; priority is a decision.</summary>
public enum DefectSeverity
{
    /// <summary>Cosmetic. No effect on use.</summary>
    Trivial,

    /// <summary>Annoying, with a workaround.</summary>
    Minor,

    /// <summary>A function does not work and there is no reasonable workaround.</summary>
    Major,

    /// <summary>Data loss, a security hole, or the product cannot be used at all.</summary>
    Critical,
}
