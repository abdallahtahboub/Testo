using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Autestiq.Contracts.Common;
using Autestiq.Contracts.Execution;
using Autestiq.Contracts.Identity;
using Autestiq.Domain.Execution;
using Autestiq.Persistence;

namespace Autestiq.Application.Execution;

/// <summary>
/// Issues and checks organizations' proofs that they control origins (Milestone 3.5). It never fetches anything:
/// a check is queued here and carried out by a worker, behind its egress proxy (<see cref="RunDispatcher"/>).
/// </summary>
internal sealed class OriginProofService(
    AutestiqDbContext database,
    ICurrentUser currentUser,
    IOrganizationContext organizationContext,
    Microsoft.Extensions.Options.IOptionsMonitor<Usage.UsageOptions> usage,
    TimeProvider timeProvider)
    : IOriginProofService
{
    /// <inheritdoc />
    public async Task<OperationResult<OriginProofRequest>> RequestAsync(string origin, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.ManageProjects))
        {
            return OperationResult.Failure<OriginProofRequest>("Execution_Error_NotAllowed");
        }

        if (Origins.Normalize(origin) is not { } normalized)
        {
            return OperationResult.Failure<OriginProofRequest>("Execution_Error_InvalidOrigin");
        }

        var proof = await FindAsync(normalized, cancellationToken).ConfigureAwait(false);

        if (proof is null)
        {
            if (await ExceededAsync(cancellationToken).ConfigureAwait(false))
            {
                return OperationResult.Failure<OriginProofRequest>("Execution_Error_TooManyStarts");
            }

            proof = new OriginProof
            {
                OrganizationId = organizationContext.RequireOrganizationId(),
                Origin = normalized,
                Token = NewToken(),
                IssuedAt = timeProvider.GetUtcNow(),
            };

            database.OriginProofs.Add(proof);
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return OperationResult.With(new OriginProofRequest(proof.Origin, proof.Token, proof.Origin + OriginProofFile.Path));
    }

    /// <inheritdoc />
    public async Task<OperationResult> CheckAsync(string origin, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.ManageProjects))
        {
            return OperationResult.Failure("Execution_Error_NotAllowed");
        }

        if (Origins.Normalize(origin) is not { } normalized)
        {
            return OperationResult.Failure("Execution_Error_InvalidOrigin");
        }

        if (await FindAsync(normalized, cancellationToken).ConfigureAwait(false) is not { } proof)
        {
            return OperationResult.Failure("Execution_Error_NoSuchProof");
        }

        if (proof.Status == OriginProofStatus.Checking)
        {
            // Already asked for and not yet done: asking again changes nothing.
            return OperationResult.Success;
        }

        if (await ExceededAsync(cancellationToken).ConfigureAwait(false))
        {
            return OperationResult.Failure("Execution_Error_TooManyStarts");
        }

        proof.Status = OriginProofStatus.Checking;
        proof.CheckRequestedAt = timeProvider.GetUtcNow();
        proof.CheckStartedAt = null;

        await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return OperationResult.Success;
    }

    /// <inheritdoc />
    public async Task<OriginProofState?> GetAsync(string origin, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Can(Permission.Read) || Origins.Normalize(origin) is not { } normalized)
        {
            return null;
        }

        var proof = await FindAsync(normalized, cancellationToken).ConfigureAwait(false);

        // A verified proof that has expired reads as what it is, without waiting for anything to rewrite the row.
        return proof is null
            ? null
            : new OriginProofState(
                proof.Origin,
                proof.Status == OriginProofStatus.Verified && !proof.CountsAt(timeProvider.GetUtcNow()) ? OriginProofStatus.Issued : proof.Status,
                proof.VerifiedAt,
                proof.ExpiresAt,
                proof.FailureReason);
    }

    /// <summary>
    /// Whether the organization has asked for too many proofs or checks in the last minute: each check sends a worker
    /// to somebody's website, so asking cannot be free (ADR-0027).
    /// </summary>
    private Task<bool> ExceededAsync(CancellationToken cancellationToken) =>
        Usage.StartLimits.ProofRequestsExceededAsync(
            database, organizationContext.RequireOrganizationId(), usage.CurrentValue, timeProvider.GetUtcNow(), cancellationToken);

    /// <summary>Through the tenant filter: the organization's own proof, never another's.</summary>
    private Task<OriginProof?> FindAsync(string normalized, CancellationToken cancellationToken) =>
        database.OriginProofs.FirstOrDefaultAsync(proof => proof.Origin == normalized, cancellationToken);

    /// <summary>256 random bits, written so that they survive a file, a URL and a copy and paste.</summary>
    private static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
