using Microsoft.Playwright;

namespace Testo.Worker.Scenarios;

/// <summary>One thing a run does, and how to tell whether it worked.</summary>
/// <param name="Description">What it does, in words a person reading the run will see.</param>
/// <param name="Run">
/// The step itself. It throws to fail: a <see cref="PlaywrightException"/> from an assertion, or
/// anything else, becomes a failed step with that message.
/// </param>
public sealed record ScenarioStep(string Description, Func<IPage, CancellationToken, Task> Run);

/// <summary>A scripted journey. No model chooses any of this (ADR-0018); Milestone 3.4 is where that starts.</summary>
/// <param name="Id">How a run names it.</param>
/// <param name="Name">What it is called.</param>
/// <param name="Description">What it does and what it checks.</param>
/// <param name="Steps">The steps, in order.</param>
public sealed record Scenario(string Id, string Name, string Description, IReadOnlyList<ScenarioStep> Steps);
