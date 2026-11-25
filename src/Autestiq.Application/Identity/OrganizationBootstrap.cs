using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Autestiq.Application.Audit;
using Autestiq.Contracts.Identity;
using Autestiq.Domain.Identity;
using Autestiq.Persistence;

namespace Autestiq.Application.Identity;

/// <summary>
/// How a deployed instance gets its first organization and owner (ADR-0040): an operator creates the organization and
/// an owner invitation, and the invitation is sent by email. No password is set by anyone but the owner, and no token
/// is printed: the owner accepts from their mailbox, as every other member does.
/// </summary>
public static partial class OrganizationBootstrap
{
    /// <summary>The audit action for an organization created by an operator.</summary>
    public const string Created = "organization.created";

    /// <summary>
    /// Creates the organization and invites its owner. Failures are reasons for the operator, not resource keys: this
    /// is an operator command, not a screen.
    /// </summary>
    public static async Task<(Guid? OrganizationId, string? Problem)> CreateAsync(
        AutestiqDbContext database,
        TenantWriteScope writeScope,
        IInvitationNotifier notifier,
        TimeProvider timeProvider,
        string slug,
        string name,
        string ownerEmail,
        string by,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(writeScope);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (!SlugPattern().IsMatch(slug))
        {
            return (null, "The slug must be 2 to 40 lowercase letters, digits and hyphens, starting with a letter.");
        }

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
        {
            return (null, "The name must be 1 to 100 characters.");
        }

        if (!System.Net.Mail.MailAddress.TryCreate(ownerEmail, out _))
        {
            return (null, "The owner's address is not an email address.");
        }

        // Organizations are outside the tenant boundary; the slug is unique across all of them.
        if (await database.Organizations.IgnoreQueryFilters().AnyAsync(organization => organization.Slug == slug, cancellationToken).ConfigureAwait(false))
        {
            return (null, $"An organization {slug} exists already.");
        }

        var now = timeProvider.GetUtcNow();
        var token = Tokens.New();
        var organization = new Organization { Name = name.Trim(), Slug = slug, DataRegion = "eu-central" };

        var invitation = new Invitation
        {
            OrganizationId = organization.Id,
            Email = ownerEmail.Trim(),
            NormalizedEmail = SignInWorkflow.Normalize(ownerEmail),
            Role = Role.Owner,
            TokenHash = Tokens.HashOf(token)!, // not null: the token was just made.
            ExpiresAt = now.AddDays(7),
            InvitedBy = Guid.Empty,
        };

        database.Organizations.Add(organization);
        database.Invitations.Add(invitation);

        var audit = new AuditWriter(database, timeProvider);
        audit.Record(organization.Id, null, null, Created, nameof(Organization), organization.Id, organization.Slug, after: new { By = by });
        audit.Record(organization.Id, null, null, AuditActions.MemberInvited, nameof(Invitation), invitation.Id, invitation.Email, after: new { Role = nameof(Role.Owner), By = by });

        using (writeScope.AllowWritingTo(organization.Id))
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        await notifier.SendInvitationAsync(invitation.Email, organization.Name, token, cancellationToken).ConfigureAwait(false);
        return (organization.Id, null);
    }

    [GeneratedRegex("^[a-z][a-z0-9-]{1,39}$")]
    private static partial Regex SlugPattern();
}
