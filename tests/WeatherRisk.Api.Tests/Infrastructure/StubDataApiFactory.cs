using Microsoft.Extensions.DependencyInjection;
using WeatherRisk.Core.Abstractions;

namespace WeatherRisk.Api.Tests.Infrastructure;

/// <summary>Shared host for hub/risk endpoint tests: real pipeline and database, deterministic stub data.</summary>
public sealed class StubDataApiFactory : IAsyncDisposable
{
    public StubDataApiFactory() => Factory = Create();

    public WeatherRiskApiFactory Factory { get; }

    /// <summary>Creates a host backed by <see cref="StubHubDataProvider"/>, with optional configuration overrides.</summary>
    public static WeatherRiskApiFactory Create(IReadOnlyDictionary<string, string?>? settings = null) => new()
    {
        Settings = settings ?? new Dictionary<string, string?>(),
        TestServices = services => services.AddScoped<IHubDataProvider, StubHubDataProvider>(),
    };

    public ValueTask DisposeAsync() => Factory.DisposeAsync();
}
