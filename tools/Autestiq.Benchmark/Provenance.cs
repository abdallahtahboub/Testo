using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Autestiq.Ai;

namespace Autestiq.Benchmark;

/// <summary>
/// Exactly what a report measured (Milestone 5.1 R5): the source revision and whether it was clean, the release
/// candidate's tag when there is one, the model, the prompt's hash, the target and how it was explored, and how it is
/// put back between runs. Written at the top of every report, so a figure can be traced to what produced it.
/// </summary>
public sealed record Provenance(
    string Revision,
    bool Clean,
    string? Tag,
    string Model,
    string PromptHash,
    string Exploration,
    string Reset)
{
    /// <summary>The first twelve hex digits of the system prompt's SHA-256.</summary>
    public static string HashOfPrompt() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(PlanPrompt.System)))[..12];

    /// <summary>The commit checked out, and whether the working tree has changes; "unknown" outside a git checkout.</summary>
    public static (string Revision, bool Clean) Source()
    {
        var revision = Git("rev-parse", "HEAD");
        // The benchmark's own reports do not count as changes: writing the first report must not make the next run
        // refuse (found in the first release-candidate measurement, 2026-11-25).
        var status = Git("status", "--porcelain", "--untracked-files=no", "--", ".", ":(exclude)docs/ai-benchmark*.md");
        return (revision ?? "unknown", revision is not null && status is { Length: 0 });
    }

    /// <summary>The tag pointing exactly at the checked-out commit, if any.</summary>
    public static string? TagAtHead() => Git("describe", "--tags", "--exact-match") is { Length: > 0 } tag ? tag : null;

    /// <summary>How each target is put back between explorations and executions.</summary>
    public static string ResetFor(string target) => target switch
    {
        "booking" => "the clinic's data is put back to its seed before every exploration and every execution (Clinic.Reset)",
        "invoices" => "the ledger is put back to its seed before every exploration and every execution (Ledger.Reset)",
        "demo" => "nothing is stored on the server: a basket lives in the browser, and every exploration and execution starts a new browser",
        _ => "not reset by the benchmark: the operator starts the application fresh before the run, and explorations and executions then see what earlier ones changed",
    };

    /// <summary>The section a report starts with.</summary>
    public string Markdown()
    {
        var text = new StringBuilder("## What was measured\n\n");
        text.Append("| | |\n|---|---|\n");
        text.Append("| Source revision | `").Append(Revision).Append('`').Append(Clean ? "" : " **with uncommitted changes**").Append(" |\n");
        text.Append("| Release candidate | ").Append(Tag is null ? "none: not a release-candidate measurement" : $"`{Tag}`").Append(" |\n");
        text.Append("| Model | ").Append(Model).Append(" |\n");
        text.Append("| System prompt (SHA-256, first 12) | `").Append(PromptHash).Append("` |\n");
        text.Append("| Exploration | ").Append(Exploration).Append(" |\n");
        text.Append("| Reset between runs | ").Append(Reset).Append(" |\n\n");
        return text.ToString();
    }

    private static string? Git(params string[] arguments)
    {
        try
        {
            var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };

            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start);

            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : null;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
