namespace Testo.Contracts.Common;

/// <summary>
/// Whether something worked, and what to tell the person if it did not.
/// </summary>
/// <remarks>
/// Failures that a person can do something about are results, not exceptions: a password that is
/// too short or an invitation that has expired is an ordinary outcome, and the screen has to show
/// it rather than an error page.
/// </remarks>
/// <param name="Succeeded">Whether it worked.</param>
/// <param name="Errors">
/// Resource keys naming what went wrong, so the message is translated where it is shown rather
/// than built in English somewhere underneath.
/// </param>
public sealed record OperationResult(bool Succeeded, IReadOnlyList<string> Errors)
{
    /// <summary>It worked.</summary>
    public static OperationResult Success { get; } = new(true, []);

    /// <summary>It did not work, for the given reasons.</summary>
    public static OperationResult Failure(params string[] errors) => new(false, errors);

    /// <summary>It worked, and produced this.</summary>
    /// <typeparam name="T">What it produced.</typeparam>
    public static OperationResult<T> With<T>(T value) => new(true, value, []);

    /// <summary>It did not work, for the given reasons, and so produced nothing.</summary>
    /// <typeparam name="T">What it would have produced.</typeparam>
    public static OperationResult<T> Failure<T>(params string[] errors) => new(false, default, errors);
}
