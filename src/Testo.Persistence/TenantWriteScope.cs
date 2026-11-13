namespace Testo.Persistence;

/// <summary>
/// A short, declared permission to write into an organization other than the current one.
/// </summary>
/// <remarks>
/// <para>
/// Most writes belong to the organization of the request that made them, and
/// <see cref="Interceptors.TenantGuardInterceptor"/> fills that in. A few genuinely do not, and
/// they all happen while nobody is signed in:
/// </para>
/// <list type="bullet">
///   <item>signing in writes an audit entry into the organization being entered;</item>
///   <item>accepting an invitation creates a membership in the organization that invited;</item>
///   <item>a password reset writes an entry into every organization that person belongs to.</item>
/// </list>
/// <para>
/// Before this existed, the guard simply trusted any organization already set on a new row
/// (D-2 in <c>docs/security-register.md</c>). Acting as one organization, a row could be written
/// into another, and the other could read it. Nothing in the product did that, but the layer was
/// not doing its job.
/// </para>
/// <para>
/// The permission lasts until the returned handle is disposed, so it reads as a block in the code
/// and shows up in review. Anything outside such a block is refused.
/// </para>
/// </remarks>
public sealed class TenantWriteScope
{
    private readonly HashSet<Guid> _allowed = [];

    /// <summary>
    /// Allows writing into these organizations until the returned handle is disposed.
    /// </summary>
    /// <param name="organizationIds">The organizations this piece of work legitimately writes to.</param>
    public IDisposable AllowWritingTo(params ReadOnlySpan<Guid> organizationIds)
    {
        var added = new List<Guid>(organizationIds.Length);

        foreach (var organizationId in organizationIds)
        {
            if (organizationId != Guid.Empty && _allowed.Add(organizationId))
            {
                added.Add(organizationId);
            }
        }

        return new Permission(this, added);
    }

    /// <summary>Whether a write into this organization has been declared.</summary>
    public bool Allows(Guid organizationId) => _allowed.Contains(organizationId);

    /// <summary>Withdraws exactly what it granted, so nested blocks do not interfere.</summary>
    private sealed class Permission(TenantWriteScope scope, List<Guid> granted) : IDisposable
    {
        public void Dispose()
        {
            foreach (var organizationId in granted)
            {
                scope._allowed.Remove(organizationId);
            }

            granted.Clear();
        }
    }
}
