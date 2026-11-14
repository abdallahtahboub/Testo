using System.Text.Json;
using Testo.Contracts.Execution;
using Testo.Worker;

// The worker: one job in on standard input, one result out on standard output, then it exits and
// its container is destroyed (ADR-0018).
//
// Nothing here knows which organization the run belongs to, and that is deliberate. The result
// names its run; the application re-reads the organization from the stored row. There is nothing
// for a compromised worker to lie about.

var cancellation = new CancellationTokenSource();

Console.CancelKeyPress += (_, eventArgs) =>
{
    // A stop is an outcome, not a crash: cancel the run and let it report what it managed.
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

var input = await Console.In.ReadToEndAsync(cancellation.Token).ConfigureAwait(false);

if (string.IsNullOrWhiteSpace(input))
{
    await Console.Error.WriteLineAsync("Testo.Worker expects a job on standard input.").ConfigureAwait(false);
    return 2;
}

WorkerJob job;

try
{
    job = JsonSerializer.Deserialize(input, WorkerJsonContext.Default.WorkerJob)
        ?? throw new JsonException("the job was null");
}
catch (JsonException exception)
{
    await Console.Error.WriteLineAsync($"The job could not be read: {exception.Message}").ConfigureAwait(false);
    return 2;
}

var result = await new ScenarioRunner(TimeProvider.System)
    .RunAsync(job, cancellation.Token)
    .ConfigureAwait(false);

await Console.Out.WriteLineAsync(JsonSerializer.Serialize(result, WorkerJsonContext.Default.WorkerResult))
    .ConfigureAwait(false);

// The exit code says whether the worker worked, not whether the test passed. A failing test is a
// successful run of the worker.
return result.Status == RunStatus.InfrastructureError ? 1 : 0;
