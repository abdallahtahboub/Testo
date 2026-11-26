using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Autestiq.Contracts.Common;
using Autestiq.Contracts.Identity;
using Autestiq.Modules.Settings.Resources;

namespace Autestiq.Modules.Settings.Pages;

/// <summary>The organization's members and its pending invitations (E1.1).</summary>
public sealed partial class MembersPage : ComponentBase
{
    /// <summary>As many as an organization of the pilot has, on one page; paging comes with E6.</summary>
    private const int PageSize = 200;

    /// <summary>Every role, lowest first, as the role list shows them.</summary>
    private static readonly Role[] Roles = Enum.GetValues<Role>();

    [Inject]
    private IMembershipService Memberships { get; set; } = null!;

    [Inject]
    private IInvitationService InvitationService { get; set; } = null!;

    [Inject]
    private ICurrentUser CurrentUser { get; set; } = null!;

    [Inject]
    private IStringLocalizer<SettingsResources> Localizer { get; set; } = null!;

    private bool Loading { get; set; } = true;

    private string? LoadError { get; set; }

    private bool Busy { get; set; }

    /// <summary>Prerendered, the page answers nothing; its controls stay disabled until they would work.</summary>
    private bool Ready => RendererInfo.IsInteractive;

    private string? Problem { get; set; }

    private string? Done { get; set; }

    private IReadOnlyList<OrganizationMember> Members { get; set; } = [];

    private IReadOnlyList<PendingInvitation> Invitations { get; set; } = [];

    /// <summary>The member whose removal is being confirmed.</summary>
    private OrganizationMember? Removing { get; set; }

    private string? InviteEmail { get; set; }

    private Role InviteRole { get; set; } = Role.QaEngineer;

    private bool CanManage => CurrentUser.Can(Permission.ManagePeople);

    /// <inheritdoc />
    protected override Task OnInitializedAsync() => CanManage ? LoadAsync() : Task.CompletedTask;

    private async Task LoadAsync()
    {
        Loading = true;
        LoadError = null;

        try
        {
            Members = (await Memberships.ListMembersAsync(1, PageSize, CancellationToken.None).ConfigureAwait(false)).Items;
            Invitations = (await InvitationService.ListPendingAsync(1, PageSize, CancellationToken.None).ConfigureAwait(false)).Items;
        }
        catch (InvalidOperationException)
        {
            LoadError = Localizer["Members_LoadFailed"];
        }
        finally
        {
            Loading = false;
        }
    }

    private Task ChangeRoleAsync(OrganizationMember member, string? value) =>
        Enum.TryParse<Role>(value, out var role) && role != member.Role
            ? DoAsync(() => Memberships.ChangeRoleAsync(member.MembershipId, role, CancellationToken.None), Localizer["Members_Done_Role", member.DisplayName, RoleName(role)])
            : Task.CompletedTask;

    private async Task RemoveAsync()
    {
        if (Removing is not { } leaving)
        {
            return;
        }

        await DoAsync(() => Memberships.RemoveMemberAsync(leaving.MembershipId, CancellationToken.None), Localizer["Members_Done_Removed", leaving.DisplayName])
            .ConfigureAwait(false);
        Removing = null;
    }

    private async Task InviteAsync()
    {
        if (string.IsNullOrWhiteSpace(InviteEmail))
        {
            Problem = Localizer["Members_Invite_EmailRequired"];
            Done = null;
            return;
        }

        var email = InviteEmail.Trim();

        if (await DoAsync(() => InvitationService.InviteAsync(email, InviteRole, CancellationToken.None), Localizer["Members_Done_Invited", email])
                .ConfigureAwait(false))
        {
            InviteEmail = null;
        }
    }

    // Inviting again replaces the earlier link with a new one, so only one is ever live (InvitationService).
    private Task<bool> ResendAsync(PendingInvitation invitation) =>
        DoAsync(() => InvitationService.InviteAsync(invitation.Email, invitation.Role, CancellationToken.None), Localizer["Members_Done_Resent", invitation.Email]);

    private Task<bool> RevokeAsync(PendingInvitation invitation) =>
        DoAsync(() => InvitationService.RevokeAsync(invitation.InvitationId, CancellationToken.None), Localizer["Members_Done_Revoked", invitation.Email]);

    /// <summary>Makes one change, says what happened in the person's language, and reads the lists again.</summary>
    private async Task<bool> DoAsync(Func<Task<OperationResult>> change, string done)
    {
        if (Busy)
        {
            return false;
        }

        Busy = true;
        Problem = null;
        Done = null;

        try
        {
            var result = await change().ConfigureAwait(false);

            if (!result.Succeeded)
            {
                // The use cases answer in resource keys, never in English (CLAUDE.md, Application).
                Problem = Localizer[ErrorKey(result.Errors[0])];
                return false;
            }

            Done = done;
            await LoadAsync().ConfigureAwait(false);
            return true;
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>A role in the person's words, spelled out so that the resource tests can find each key.</summary>
    private string RoleName(Role role) => Localizer[role switch
    {
        Role.Viewer => "Role_Viewer",
        Role.Developer => "Role_Developer",
        Role.QaEngineer => "Role_QaEngineer",
        Role.QaLead => "Role_QaLead",
        Role.Admin => "Role_Admin",
        Role.Owner => "Role_Owner",
        _ => "Role_Viewer",
    }];

    /// <summary>The services' error keys, spelled out so that the resource tests can find each one.</summary>
    private static string ErrorKey(string error) => error switch
    {
        "Members_Error_AboveYourRole" => "Members_Error_AboveYourRole",
        "Members_Error_AlreadyAMember" => "Members_Error_AlreadyAMember",
        "Members_Error_CannotChangeOwnRole" => "Members_Error_CannotChangeOwnRole",
        "Members_Error_CannotRemoveYourself" => "Members_Error_CannotRemoveYourself",
        "Members_Error_InvitationNotFound" => "Members_Error_InvitationNotFound",
        "Members_Error_LastOwner" => "Members_Error_LastOwner",
        "Members_Error_NotFound" => "Members_Error_NotFound",
        _ => "Members_Error_NotAllowed",
    };
}
