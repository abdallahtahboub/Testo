using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Testo.Persistence;

/// <summary>
/// Renames tables, columns, keys and indexes to <c>snake_case</c>.
/// </summary>
/// <remarks>
/// PostgreSQL folds unquoted identifiers to lower case, so a column left as <c>CreatedAt</c> has
/// to be quoted in every hand-written query and in every psql session. Thirty lines here save
/// that for the life of the product, and avoid a package for something this small.
/// </remarks>
internal static class SnakeCaseNames
{
    public static void ApplyTo(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (tableName is not null)
            {
                entity.SetTableName(ToSnakeCase(tableName));
            }

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.GetColumnName()));
            }

            foreach (var key in entity.GetKeys())
            {
                key.SetName(ToSnakeCase(key.GetName()!));
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                foreignKey.SetConstraintName(ToSnakeCase(foreignKey.GetConstraintName()!));
            }

            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName()!));
            }
        }
    }

    /// <summary>
    /// <c>OrganizationId</c> becomes <c>organization_id</c>; <c>IPAddress</c> becomes
    /// <c>ip_address</c> rather than <c>i_p_address</c>.
    /// </summary>
    internal static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var builder = new StringBuilder(name.Length + 8);

        for (var index = 0; index < name.Length; index++)
        {
            var current = name[index];

            if (current == '_')
            {
                builder.Append('_');
                continue;
            }

            if (char.IsUpper(current) && index > 0 && NeedsSeparatorBefore(name, index))
            {
                builder.Append('_');
            }

            builder.Append(char.ToLower(current, CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    /// <summary>
    /// A separator goes before an upper-case letter that starts a word: one that follows a
    /// lower-case letter or a digit, or one that ends a run of capitals, as the <c>A</c> of
    /// <c>IPAddress</c> does.
    /// </summary>
    private static bool NeedsSeparatorBefore(string name, int index)
    {
        var previous = name[index - 1];

        if (previous == '_')
        {
            return false;
        }

        if (!char.IsUpper(previous))
        {
            return true;
        }

        return index + 1 < name.Length && char.IsLower(name[index + 1]);
    }
}
