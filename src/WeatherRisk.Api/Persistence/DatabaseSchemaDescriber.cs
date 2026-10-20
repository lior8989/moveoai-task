using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using WeatherRisk.Api.Contracts.System;

namespace WeatherRisk.Api.Persistence;

/// <summary>Builds a <see cref="DatabaseSchemaResponse"/> from the EF Core model.</summary>
public static class DatabaseSchemaDescriber
{
    /// <summary>Describes every table mapped by the context.</summary>
    /// <param name="db">Any DbContext; its design-time model is read (the runtime model strips comments).</param>
    /// <returns>Tables, columns, indexes, foreign keys and comments.</returns>
    public static DatabaseSchemaResponse Describe(DbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        IModel model = db.GetService<IDesignTimeModel>().Model;

        var tables = model.GetEntityTypes()
            .Where(e => e.GetTableName() is not null)
            .Select(DescribeTable)
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

        return new DatabaseSchemaResponse(db.Database.ProviderName ?? "unknown", tables);
    }

    private static TableSchema DescribeTable(IEntityType entity)
    {
        string table = entity.GetTableName()!;
        var store = StoreObjectIdentifier.Table(table, entity.GetSchema());

        var columns = entity.GetProperties()
            .Select(p => new ColumnSchema(
                p.GetColumnName(store) ?? p.Name,
                p.GetColumnType(store),
                p.IsColumnNullable(store),
                p.IsPrimaryKey(),
                p.GetComment()))
            .ToList();

        var indexes = entity.GetIndexes()
            .Select(i => new IndexSchema(
                i.GetDatabaseName(store) ?? i.Name ?? string.Empty,
                i.Properties.Select(p => p.GetColumnName(store) ?? p.Name).ToList(),
                i.IsUnique))
            .ToList();

        var foreignKeys = entity.GetForeignKeys()
            .Select(fk => new ForeignKeySchema(
                fk.Properties.Select(p => p.GetColumnName(store) ?? p.Name).ToList(),
                fk.PrincipalEntityType.GetTableName() ?? fk.PrincipalEntityType.ShortName(),
                fk.PrincipalKey.Properties.Select(p => p.GetColumnName() ?? p.Name).ToList(),
                fk.DeleteBehavior.ToString()))
            .ToList();

        return new TableSchema(table, entity.GetComment(), columns, indexes, foreignKeys);
    }
}
