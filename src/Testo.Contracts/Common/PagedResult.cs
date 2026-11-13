namespace Testo.Contracts.Common;

/// <summary>
/// One page of a longer list, with enough information for the interface to say "41 to 60 of 237"
/// and to decide whether the next-page control is reachable.
/// </summary>
/// <typeparam name="T">What the page holds.</typeparam>
/// <param name="Items">The rows on this page.</param>
/// <param name="TotalCount">How many rows there are in total, across every page.</param>
/// <param name="Page">The page number, counting from one.</param>
/// <param name="PageSize">How many rows a full page holds.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    /// <summary>How many pages there are in total.</summary>
    public int PageCount => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>Whether a page follows this one.</summary>
    public bool HasNextPage => Page < PageCount;

    /// <summary>Whether a page precedes this one.</summary>
    public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// Makes an empty page. A companion to <see cref="PagedResult{T}"/> rather than a member of it,
/// because a static member on a generic type has to be written out in full at every use.
/// </summary>
public static class PagedResult
{
    /// <summary>An empty page, for the moment before anything has loaded.</summary>
    /// <typeparam name="T">What the page would hold.</typeparam>
    public static PagedResult<T> Empty<T>() => new([], 0, 1, 0);
}
