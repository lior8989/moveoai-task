using Microsoft.Data.Sqlite;

namespace WeatherRisk.Api.Persistence;

/// <summary>Resolves the configured SQLite connection string into an absolute, ready-to-use one.</summary>
public static class SqliteConnectionStrings
{
    /// <summary>Configuration key of the connection string (<c>ConnectionStrings:WeatherRisk</c>).</summary>
    public const string Name = "WeatherRisk";

    /// <summary>
    /// Makes a relative <c>Data Source</c> absolute under <paramref name="contentRoot"/> and creates its folder,
    /// so the database location doesn't depend on the current working directory.
    /// </summary>
    /// <param name="connectionString">Configured connection string.</param>
    /// <param name="contentRoot">Application content root (the Api project folder when run locally).</param>
    /// <returns>Connection string with an absolute data source; in-memory databases are returned unchanged.</returns>
    /// <exception cref="InvalidOperationException">When the connection string has no data source.</exception>
    public static string Resolve(string connectionString, string contentRoot)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource))
        {
            throw new InvalidOperationException($"Connection string '{Name}' has no Data Source.");
        }

        if (builder.DataSource == ":memory:" || builder.Mode == SqliteOpenMode.Memory)
        {
            return connectionString;
        }

        if (!Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.GetFullPath(Path.Combine(contentRoot, builder.DataSource));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(builder.DataSource)!);
        return builder.ToString();
    }
}
