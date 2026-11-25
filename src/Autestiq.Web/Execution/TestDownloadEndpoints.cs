using System.Text;
using Autestiq.Contracts.Testing;

namespace Autestiq.Web.Execution;

/// <summary>Serves an approved test as a file, to somebody signed in who may see it (Milestone 5, part 4).</summary>
public static class TestDownloadEndpoints
{
    /// <summary>Where it is fetched from; <see cref="Platform.Routes.TestDownload"/> builds it.</summary>
    public const string Pattern = "/app/tests/{testCaseId:guid}/download";

    /// <summary>Adds the endpoint.</summary>
    public static IEndpointRouteBuilder MapTestDownloadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(Pattern, ServeAsync).RequireAuthorization();

        return endpoints;
    }

    /// <summary>
    /// Every check is the use case's: the organization (through the tenant filter), the permission, and an approval of
    /// this very plan. Any refusal is "not found", the answer a test that never existed gets, so nothing leaks.
    /// </summary>
    private static async Task<IResult> ServeAsync(Guid testCaseId, ITestCaseService tests, CancellationToken cancellationToken)
    {
        var result = await tests.DownloadAsync(testCaseId, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded || result.Value is not { } exported)
        {
            return Results.NotFound();
        }

        // A download, never shown in the browser: a file is not a page, and plain text is never run as one.
        return Results.File(Encoding.UTF8.GetBytes(exported.Code), "text/x-python; charset=utf-8", exported.FileName);
    }
}
