namespace WeatherRisk.Api.Contracts.System;

/// <summary>Live description of the database schema, generated from the EF Core model (cannot drift from the code).</summary>
/// <param name="Provider">Database provider, e.g. <c>Microsoft.EntityFrameworkCore.Sqlite</c>.</param>
/// <param name="Tables">Tables in alphabetical order.</param>
public sealed record DatabaseSchemaResponse(string Provider, IReadOnlyList<TableSchema> Tables);

/// <summary>One table.</summary>
/// <param name="Name">Table name.</param>
/// <param name="Comment">What the table stores.</param>
/// <param name="Columns">Columns in model order.</param>
/// <param name="Indexes">Secondary indexes (the primary key is marked on columns).</param>
/// <param name="ForeignKeys">Foreign keys to other tables.</param>
public sealed record TableSchema(
    string Name,
    string? Comment,
    IReadOnlyList<ColumnSchema> Columns,
    IReadOnlyList<IndexSchema> Indexes,
    IReadOnlyList<ForeignKeySchema> ForeignKeys);

/// <summary>One column.</summary>
/// <param name="Name">Column name.</param>
/// <param name="Type">Store type, e.g. <c>TEXT</c>, <c>REAL</c>, <c>INTEGER</c>.</param>
/// <param name="Nullable">Whether NULL is allowed.</param>
/// <param name="PrimaryKey">Whether the column is part of the primary key.</param>
/// <param name="Comment">Meaning and unit of the column.</param>
public sealed record ColumnSchema(string Name, string Type, bool Nullable, bool PrimaryKey, string? Comment);

/// <summary>One index.</summary>
/// <param name="Name">Index name.</param>
/// <param name="Columns">Indexed columns in order.</param>
/// <param name="Unique">Whether the index is unique.</param>
public sealed record IndexSchema(string Name, IReadOnlyList<string> Columns, bool Unique);

/// <summary>One foreign key.</summary>
/// <param name="Columns">Referencing columns.</param>
/// <param name="PrincipalTable">Referenced table.</param>
/// <param name="PrincipalColumns">Referenced columns.</param>
/// <param name="OnDelete">Delete behaviour, e.g. <c>Cascade</c>.</param>
public sealed record ForeignKeySchema(
    IReadOnlyList<string> Columns, string PrincipalTable, IReadOnlyList<string> PrincipalColumns, string OnDelete);
