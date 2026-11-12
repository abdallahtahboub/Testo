using System.Reflection;
using Microsoft.AspNetCore.Components;
using Testo.Platform;
using Testo.Platform.Localization;
using Testo.Website.Pages;

namespace Testo.UI.Tests.Architecture;

/// <summary>
/// Checks the URL rules: public pages have one address per language and are static
/// (ADR-0003, ADR-0006); all other pages live below <c>/app</c>.
/// </summary>
public class PageRouteTests
{
    /// <summary>Pages that report a status. They are reached by re-executing a request, not by a link.</summary>
    private static readonly string[] StatusRoutes = [Routes.NotFound, Routes.Error];

    private static readonly Assembly Website = typeof(HomePage).Assembly;

    private static IEnumerable<Type> Pages(Assembly assembly) =>
        assembly.GetExportedTypes().Where(type => type.GetCustomAttributes<RouteAttribute>().Any());

    private static string[] RoutesOf(Type page) =>
        [.. page.GetCustomAttributes<RouteAttribute>().Select(route => route.Template)];

    public static TheoryData<Type> PublicPages => [.. Pages(Website)];

    public static TheoryData<Type> AppPages =>
    [
        .. SourceProjects.Names
            .Where(name => name != "Testo.Website")
            .Select(Assembly.Load)
            .SelectMany(Pages),
    ];

    [Fact]
    public void PublicPages_Exist()
    {
        Assert.NotEmpty(PublicPages);
        Assert.NotEmpty(AppPages);
    }

    [Theory]
    [MemberData(nameof(PublicPages))]
    public void PublicPage_IsRenderedStatically(Type page)
    {
        Assert.NotNull(page.GetCustomAttribute<ExcludeFromInteractiveRoutingAttribute>());
    }

    [Theory]
    [MemberData(nameof(PublicPages))]
    public void PublicPage_HasOneAddressPerLanguage(Type page)
    {
        var routes = RoutesOf(page);
        if (routes.All(route => StatusRoutes.Contains(route)))
        {
            return;
        }

        var languages = new List<string>();
        var paths = new List<string>();
        foreach (var route in routes)
        {
            Assert.True(LocalizedPath.TryGetCulture(route, out var culture), $"{page.Name}: the route '{route}' has no language prefix.");
            languages.Add(culture);
            paths.Add(route[(1 + culture.Length)..]);
        }

        Assert.Equal(SupportedCultures.All.Order(), languages.Order());
        Assert.Single(paths.Distinct());
    }

    [Theory]
    [MemberData(nameof(AppPages))]
    public void AppPage_LivesBelowTheAppRootAndIsInteractive(Type page)
    {
        Assert.All(RoutesOf(page), route =>
            Assert.True(route == Routes.App || route.StartsWith(Routes.App + "/", StringComparison.Ordinal), $"{page.Name}: the route '{route}' is not below {Routes.App}."));
        Assert.Null(page.GetCustomAttribute<ExcludeFromInteractiveRoutingAttribute>());
    }

    [Theory]
    [MemberData(nameof(PublicPages))]
    [MemberData(nameof(AppPages))]
    public void Page_Routes_AreLowercase(Type page)
    {
        // Route parameters such as {projectKey} are exempt.
        Assert.All(RoutesOf(page), route =>
        {
            var literal = System.Text.RegularExpressions.Regex.Replace(route, @"\{[^}]*\}", "");
            Assert.DoesNotContain(literal, char.IsUpper);
        });
    }
}
