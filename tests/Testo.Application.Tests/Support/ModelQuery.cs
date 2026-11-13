using System.Collections;
using Microsoft.EntityFrameworkCore;
using Testo.Domain.Common;

namespace Testo.Application.Tests.Support;

/// <summary>
/// Reads a table whose type is only known at run time, so that the isolation test can ask the
/// same question of every entity rather than of a list somebody maintains by hand.
/// </summary>
internal static class ModelQuery
{
    /// <summary>
    /// Every row of this kind the current organization can see, with the query filter in force.
    /// </summary>
    public static List<IBelongsToOrganization> VisibleRows(this DbContext database, Type entityType)
    {
        var set = typeof(DbContext)
            .GetMethods()
            .Single(method => method.Name == nameof(DbContext.Set) && method.IsGenericMethod && method.GetParameters().Length == 0)
            .MakeGenericMethod(entityType)
            .Invoke(database, null);

        return [.. ((IEnumerable)set!).Cast<IBelongsToOrganization>()];
    }
}
