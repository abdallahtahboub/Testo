using Microsoft.AspNetCore.Localization;
using Testo.Platform.Localization;

namespace Testo.Web.Localization;

/// <summary>Configures how the language of a request is determined.</summary>
internal static class LocalizationSetup
{
    public static IServiceCollection AddTestoLocalization(this IServiceCollection services)
    {
        services.AddLocalization();
        services.Configure<RequestLocalizationOptions>(options =>
        {
            var cultures = SupportedCultures.All.ToArray();
            options
                .SetDefaultCulture(SupportedCultures.Default)
                .AddSupportedCultures(cultures)
                .AddSupportedUICultures(cultures);
            options.ApplyCurrentCultureToResponseHeaders = true;

            // Language sources, in this order: the URL prefix (public site), the preference
            // cookie (set by the language switch), the browser's Accept-Language header.
            // The built-in "?culture=" query string is removed: it would give every page a
            // second address per language.
            var queryString = options.RequestCultureProviders.OfType<QueryStringRequestCultureProvider>().ToList();
            foreach (var provider in queryString)
            {
                options.RequestCultureProviders.Remove(provider);
            }

            options.RequestCultureProviders.Insert(0, new UrlPrefixRequestCultureProvider());
        });

        return services;
    }
}
