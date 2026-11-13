using Testo.E2E.Support;
using Testo.Platform;

namespace Testo.E2E.Http;

/// <summary>
/// The account pages answer a plain request. Checked at this level as well as through a browser,
/// because a page that never finishes rendering looks like a browser problem from up there.
/// </summary>
public class AccountPageHttpTests(AppFixture app) : HttpTest(app)
{
    [Theory]
    [InlineData(Routes.SignIn)]
    [InlineData(Routes.SignOut)]
    [InlineData(Routes.ForgotPassword)]
    [InlineData(Routes.ResetPassword)]
    [InlineData(Routes.TwoFactor)]
    [InlineData(Routes.AcceptInvitation)]
    public async Task AccountPage_Always_Answers(string path)
    {
        using var response = await GetAsync(path);

        Assert.True(
            response.IsSuccessStatusCode,
            $"{path} answered {(int)response.StatusCode} {response.StatusCode}.");
    }
}
