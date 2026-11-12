namespace Testo.Platform.Capabilities;

/// <summary>Gives access to the registered test capabilities.</summary>
public interface ICapabilityRegistry
{
    /// <summary>All registered capabilities.</summary>
    IReadOnlyList<ITestCapability> Capabilities { get; }

    /// <summary>Finds a capability by its id, or returns <see langword="null"/>.</summary>
    ITestCapability? Find(string id);
}
