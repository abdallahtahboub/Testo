using Autestiq.Contracts.Planning;

namespace Autestiq.Application.Testing;

/// <summary>
/// Makes the planner the product uses, at the prices the price book gives for its model. Implemented by the
/// composition root, which alone holds the provider's key; <c>null</c> when no key is configured, and planning then
/// fails saying so rather than calling anything.
/// </summary>
public interface IPlannerFactory
{
    /// <summary>A planner for the model at these prices per million tokens, or <c>null</c> when none can be made.</summary>
    IScenarioPlanner? Create(string model, decimal inputPerMillion, decimal outputPerMillion);
}

/// <summary>The factory when no provider is configured: there is no planner.</summary>
internal sealed class NoPlannerFactory : IPlannerFactory
{
    public IScenarioPlanner? Create(string model, decimal inputPerMillion, decimal outputPerMillion) => null;
}
