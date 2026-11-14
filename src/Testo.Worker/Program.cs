// The worker's entry point. Filled in with the scenario runner; for now it refuses to do anything
// without a job, so that running it by accident does nothing surprising.

Console.Error.WriteLine("Testo.Worker expects a job on standard input.");
return 1;
