using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WeatherRisk.Data.Persistence;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> create the context without starting the API.
/// Not used at runtime.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<WeatherRiskDbContext>
{
    /// <inheritdoc />
    public WeatherRiskDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<WeatherRiskDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
