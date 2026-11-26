using Autestiq.E2E.Support;
using Autestiq.Platform;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace Autestiq.E2E.Pages;

/// <summary>
/// Members and invitations in a real browser (E1.1): an owner invites, sends the invitation again and withdraws it;
/// a viewer is offered none of it, neither in the navigation nor on the page.
/// </summary>
public class MembersTests(AppFixture app) : BrowserTest(app)
{
    [Fact]
    public async Task Owner_InvitesResendsAndRevokes_AndThePendingListFollows()
    {
        var page = await OpenSignedInAsync(Routes.Members);
        await Expect(page.Page.GetByTestId("invite")).ToBeEnabledAsync();

        await Expect(page.Page.Locator("h1")).ToHaveTextAsync("Members");
        await Expect(page.Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Members", Exact = true })).ToBeVisibleAsync();
        await Expect(page.Page.GetByTestId("member-mara@northwind.example")).ToContainTextAsync("Mara Lindqvist");

        var email = $"lena-{Guid.CreateVersion7():N}"[..16] + "@northwind.example";
        await page.Page.GetByTestId("invite-email").FillAsync(email);
        await page.Page.GetByTestId("invite-role").SelectOptionAsync("QaLead");
        await page.Page.GetByTestId("invite").ClickAsync();

        await Expect(page.Page.GetByTestId("members-done")).ToHaveTextAsync($"An invitation was sent to {email}.");
        await Expect(page.Page.GetByTestId($"pending-{email}")).ToBeVisibleAsync();
        await Expect(page.Page.GetByTestId("invite-email")).ToHaveValueAsync("");

        await page.Page.GetByTestId($"resend-{email}").ClickAsync();
        await Expect(page.Page.GetByTestId("members-done")).ToContainTextAsync("the earlier link no longer works");
        await Expect(page.Page.GetByTestId($"pending-{email}")).ToHaveCountAsync(1);

        await page.Page.GetByTestId($"revoke-{email}").ClickAsync();
        await Expect(page.Page.GetByTestId("members-done")).ToContainTextAsync("was withdrawn");
        await Expect(page.Page.GetByTestId($"pending-{email}")).ToHaveCountAsync(0);

        page.AssertNoProblems();
    }

    [Fact]
    public async Task Owner_InvitingSomebodyAlreadyAMember_IsRefusedInWords()
    {
        var page = await OpenSignedInAsync(Routes.Members);
        await Expect(page.Page.GetByTestId("invite")).ToBeEnabledAsync();

        await page.Page.GetByTestId("invite-email").FillAsync("sven@northwind.example");
        await page.Page.GetByTestId("invite").ClickAsync();

        await Expect(page.Page.GetByTestId("members-problem")).ToBeVisibleAsync();
        await Expect(page.Page.GetByTestId("pending-sven@northwind.example")).ToHaveCountAsync(0);
        page.AssertNoProblems();
    }

    [Fact]
    public async Task Viewer_IsOfferedNothing_NeitherInTheNavigationNorOnThePage()
    {
        var page = await NewPageAsync();
        await page.Page.GotoAsync(App.Url(Routes.SignIn));
        await page.Page.FillAsync("input[name='Input.Email']", "claudia@northwind.example");
        await page.Page.FillAsync("input[name='Input.Password']", AppFixture.SeededPassword);
        await page.Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Sign in", Exact = true }).ClickAsync();
        await Expect(page.Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(System.Text.RegularExpressions.Regex.Escape(Routes.App) + "$"));

        // The navigation has drawn (Projects is offered to everyone), and Members is not in it.
        await Expect(page.Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Projects", Exact = true })).ToBeVisibleAsync();
        await Expect(page.Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Members", Exact = true })).ToHaveCountAsync(0);

        await page.Page.GotoAsync(App.Url(Routes.Members));

        await Expect(page.Page.GetByTestId("members-not-allowed")).ToBeVisibleAsync();
        await Expect(page.Page.GetByTestId("invite")).ToHaveCountAsync(0);
        await Expect(page.Page.GetByTestId("member-mara@northwind.example")).ToHaveCountAsync(0);
        page.AssertNoProblems();
    }
}
