namespace Testo.Contracts.Common;

/// <summary>
/// Whether something worked and, if it did, what it produced.
/// </summary>
/// <typeparam name="T">What a success carries.</typeparam>
/// <param name="Succeeded">Whether it worked.</param>
/// <param name="Value">What it produced, when it did.</param>
/// <param name="Errors">Resource keys naming what went wrong, when it did not.</param>
public sealed record OperationResult<T>(bool Succeeded, T? Value, IReadOnlyList<string> Errors);
