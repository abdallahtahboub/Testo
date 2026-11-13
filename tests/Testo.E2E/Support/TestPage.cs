using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace Testo.E2E.Support;

/// <summary>
/// A browser page that records everything that goes wrong while a test uses it: console
/// errors, script errors, failed requests and error responses.
/// </summary>
public sealed class TestPage
{
    /// <summary>
    /// Playwright raises its events on its own threads, so everything below is appended from
    /// somewhere other than the test and read by the test. Without this lock a page that is still
    /// loading can make a test fail with "collection was modified", which looks like a product
    /// fault and is not one.
    /// </summary>
    private readonly Lock _gate = new();

    private readonly List<string> _problems = [];
    private readonly List<string> _webSockets = [];
    private readonly List<string> _requests = [];

    public TestPage(IPage page, Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(baseAddress);

        Page = page;
        var origin = baseAddress.GetLeftPart(UriPartial.Authority);

        page.Console += (_, message) =>
        {
            if (message.Type is "error" or "warning")
            {
                lock (_gate)
                {
                    _problems.Add($"console {message.Type}: {message.Text}");
                }
            }
        };
        page.PageError += (_, error) => Record($"script error: {error}");
        page.RequestFailed += (_, request) => Record($"request failed: {request.Url} ({request.Failure})");
        page.Response += (_, response) =>
        {
            if (response.Status >= 400)
            {
                Record($"HTTP {response.Status}: {response.Url.Replace(origin, "", StringComparison.Ordinal)}");
            }
        };
        page.Request += (_, request) =>
        {
            lock (_gate)
            {
                _requests.Add(request.Url.Replace(origin, "", StringComparison.Ordinal));
            }
        };

        page.WebSocket += (_, socket) =>
        {
            lock (_gate)
            {
                _webSockets.Add(socket.Url);
            }
        };
    }

    /// <summary>Adds a problem, from whichever thread noticed it.</summary>
    private void Record(string problem)
    {
        lock (_gate)
        {
            _problems.Add(problem);
        }
    }

    /// <summary>
    /// Whether a problem was caused by the accessibility scanner rather than by the page.
    /// </summary>
    private static bool IsScannerNoise(string problem) =>
        problem.Contains("violates the following Content Security Policy", StringComparison.Ordinal)
        || problem.Contains("Couldn't load preload assets", StringComparison.Ordinal);

    /// <summary>The Playwright page.</summary>
    public IPage Page { get; }

    /// <summary>
    /// The site-relative URLs the page has requested so far. A snapshot: the page may be loading
    /// still, and a list that grows while a test reads it fails the test for the wrong reason.
    /// </summary>
    public IReadOnlyList<string> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>The WebSocket connections the page has opened so far, as a snapshot.</summary>
    public IReadOnlyList<string> WebSockets
    {
        get
        {
            lock (_gate)
            {
                return [.. _webSockets];
            }
        }
    }

    /// <summary>The current URL without scheme and host, for example <c>/de/pricing</c>.</summary>
    public string Path => new Uri(Page.Url).PathAndQuery;

    /// <summary>
    /// Fails when the browser reported a problem. <paramref name="expected"/> lists text
    /// fragments of problems the test caused on purpose (for example the 404 of a page that
    /// does not exist).
    /// </summary>
    public void AssertNoProblems(params string[] expected)
    {
        List<string> unexpected;

        lock (_gate)
        {
            unexpected =
            [
                .. _problems.Where(problem =>
                    !expected.Any(fragment => problem.Contains(fragment, StringComparison.Ordinal))),
            ];
        }

        Assert.True(unexpected.Count == 0, "The browser reported problems:\n" + string.Join('\n', unexpected));
    }

    /// <summary>
    /// Runs the axe accessibility rules for WCAG 2.2 A and AA and fails on any serious or
    /// critical violation.
    /// </summary>
    public async Task AssertAccessibleAsync()
    {
        var options = new AxeRunOptions
        {
            RunOnly = new RunOnlyOptions
            {
                Type = "tag",
                Values = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "wcag22aa", "best-practice"],
            },
        };

        // The scanner measures contrast by styling probe elements, which a strict content
        // security policy refuses. Those refusals are the test tool's, not the page's, so the
        // ones it causes while scanning are dropped afterwards. Anything the page itself did is
        // already in the list and stays there.
        int before;

        lock (_gate)
        {
            before = _problems.Count;
        }

        var result = await Page.RunAxe(options);

        lock (_gate)
        {
            for (var index = _problems.Count - 1; index >= before; index--)
            {
                if (IsScannerNoise(_problems[index]))
                {
                    _problems.RemoveAt(index);
                }
            }
        }
        var violations = result.Violations
            .Where(violation => violation.Impact is "serious" or "critical")
            .Select(violation =>
                $"[{violation.Impact}] {violation.Id}: {violation.Help}\n" +
                string.Join('\n', violation.Nodes.Select(node => $"    {node.Target}  {node.Html}")))
            .ToList();

        Assert.True(violations.Count == 0, $"Accessibility violations on {Path}:\n" + string.Join('\n', violations));
    }
}
