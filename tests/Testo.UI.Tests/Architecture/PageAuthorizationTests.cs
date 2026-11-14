using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Testo.Platform;
using Testo.UI.Tests.Support;
using Testo.Website.Pages;

namespace Testo.UI.Tests.Architecture;

/// <summary>
/// Every routable page says, in its own source, whether it needs a signed-in person.
/// </summary>
/// <remarks>
/// <para>
/// Milestone 3 built sign-in and never fitted the gate it exists to operate: <c>/app</c> answered
/// a request with no session (D-1 in <c>docs/security-register.md</c>). No test caught it, because
/// every test either signed in first or exercised a service that already fails closed.
/// </para>
/// <para>
/// A fallback authorization policy would be the usual answer and does not fit a Blazor Web App:
/// the catch-all endpoint that serves unknown URLs would be challenged too, turning a public 404
/// into a redirect to sign-in. So the rule is enforced here instead. A page that declares neither
/// attribute fails the build, which is why it cannot be forgotten.
/// </para>
/// </remarks>
public class PageAuthorizationTests
{
    private static readonly Assembly Website = typeof(HomePage).Assembly;

    /// <summary>Every routable page, in the host, the website and every module.</summary>
    private static IEnumerable<Type> RoutablePages =>
        SourceProjects.LoadableAssemblies
            .SelectMany(assembly => assembly.GetExportedTypes())
            .Where(IsRoutable);

    public static TheoryData<Type> AllPages => [.. RoutablePages];

    public static TheoryData<Type> PublicPages => [.. Website.GetExportedTypes().Where(IsRoutable)];

    [Fact]
    public void Pages_Exist() => Assert.NotEmpty(AllPages);

    [Theory]
    [MemberData(nameof(AllPages))]
    public void Page_Always_SaysWhetherItNeedsASignedInPerson(Type page)
    {
        var requiresSignIn = Declares<AuthorizeAttribute>(page);
        var open = Declares<AllowAnonymousAttribute>(page);

        Assert.True(
            requiresSignIn || open,
            $"{page.FullName} declares neither [Authorize] nor [AllowAnonymous]. Decide which, in the "
            + "page or in the _Imports.razor of its folder. A page that says nothing is reachable by "
            + "anyone, which is how D-1 happened.");

        Assert.False(
            requiresSignIn && open,
            $"{page.FullName} declares both [Authorize] and [AllowAnonymous]. Which wins is a detail of "
            + "the framework, and a reader should not have to know it.");
    }

    [Theory]
    [MemberData(nameof(AllPages))]
    public void AppPage_Always_RequiresASignedInPerson(Type page)
    {
        var routes = page.GetCustomAttributes<RouteAttribute>().Select(route => route.Template).ToList();

        if (!routes.Any(route => route == Routes.App || route.StartsWith(Routes.App + "/", StringComparison.Ordinal)))
        {
            return;
        }

        Assert.True(
            Declares<AuthorizeAttribute>(page),
            $"{page.FullName} is under {Routes.App} and must require a signed-in person.");
    }

    [Theory]
    [MemberData(nameof(PublicPages))]
    public void PublicPage_Always_StaysOpen(Type page) =>
        Assert.True(
            Declares<AllowAnonymousAttribute>(page),
            $"{page.FullName} is on the public website, which nobody signs in to read.");

    [Fact]
    public void AccountPages_Always_StayOpen()
    {
        // The pages somebody uses when they have no session, or cannot get one.
        var account = RoutablePages
            .Where(page => page.GetCustomAttributes<RouteAttribute>()
                .Any(route => route.Template.StartsWith(Routes.Account + "/", StringComparison.Ordinal)))
            .ToList();

        Assert.NotEmpty(account);
        Assert.All(account, page => Assert.True(Declares<AllowAnonymousAttribute>(page), page.FullName));
    }

    private static bool IsRoutable(Type type) => type.GetCustomAttributes<RouteAttribute>().Any();

    /// <summary>
    /// Whether the attribute is on the page. An <c>_Imports.razor</c> puts it on every component in
    /// the folder, which is how whole areas declare it once.
    /// </summary>
    private static bool Declares<TAttribute>(Type page)
        where TAttribute : Attribute =>
        page.GetCustomAttribute<TAttribute>(inherit: true) is not null;
}
