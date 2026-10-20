using Microsoft.Data.Sqlite;
using WeatherRisk.Api.Persistence;

namespace WeatherRisk.Api.Tests.Persistence;

public sealed class SqliteConnectionStringsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"wr-root-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Resolve_RelativePath_BecomesAbsoluteUnderContentRootAndFolderIsCreated()
    {
        string resolved = SqliteConnectionStrings.Resolve("Data Source=App_Data/test.db", _root);

        string dataSource = new SqliteConnectionStringBuilder(resolved).DataSource;
        Assert.Equal(Path.Combine(_root, "App_Data", "test.db"), dataSource);
        Assert.True(Directory.Exists(Path.Combine(_root, "App_Data")));
    }

    [Fact]
    public void Resolve_AbsolutePath_IsKept()
    {
        string absolute = Path.Combine(_root, "abs.db");

        string resolved = SqliteConnectionStrings.Resolve($"Data Source={absolute}", "C:\\elsewhere");

        Assert.Equal(absolute, new SqliteConnectionStringBuilder(resolved).DataSource);
    }

    [Fact]
    public void Resolve_InMemory_IsReturnedUnchanged()
    {
        Assert.Equal("Data Source=:memory:", SqliteConnectionStrings.Resolve("Data Source=:memory:", _root));
    }

    [Fact]
    public void Resolve_NoDataSource_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => SqliteConnectionStrings.Resolve("Cache=Shared", _root));
    }
}
