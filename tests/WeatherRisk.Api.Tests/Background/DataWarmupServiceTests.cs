using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WeatherRisk.Api.Background;
using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Domain;
using WeatherRisk.Data.External;

namespace WeatherRisk.Api.Tests.Background;

public sealed class DataWarmupServiceTests
{
    private static readonly Hub Denver = new("denver", "Denver Hub", "Denver", "CO", Region.West, 39.7, -105.0, "08031");

    private static async Task RunAsync(FakeProvider provider, bool enabled)
    {
        ServiceProvider services = new ServiceCollection()
            .AddSingleton<IHubRepository>(new FakeHubs())
            .AddSingleton<IHubDataProvider>(provider)
            .BuildServiceProvider();
        var service = new DataWarmupService(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new HubDataSettings { WarmUpOnStartup = enabled }),
            NullLogger<DataWarmupService>.Instance);

        await service.StartAsync(TestContext.Current.CancellationToken);
        await (service.ExecuteTask ?? Task.CompletedTask);
        await services.DisposeAsync();
    }

    [Fact]
    public async Task Enabled_LoadsBaselinesAndWeatherForAllHubs()
    {
        var provider = new FakeProvider();

        await RunAsync(provider, enabled: true);

        Assert.Equal((1, 1), (provider.BaselineCalls, provider.WeatherCalls));
    }

    [Fact]
    public async Task Disabled_DoesNothing()
    {
        var provider = new FakeProvider();

        await RunAsync(provider, enabled: false);

        Assert.Equal((0, 0), (provider.BaselineCalls, provider.WeatherCalls));
    }

    [Fact]
    public async Task ProviderThrows_IsLoggedNotPropagated()
    {
        var provider = new FakeProvider { Throw = true };

        await RunAsync(provider, enabled: true); // Must not throw: a failed warm-up must never crash the host.

        Assert.Equal(1, provider.BaselineCalls);
    }

    private sealed class FakeHubs : IHubRepository
    {
        public Task<IReadOnlyList<Hub>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Hub>>([Denver]);

        public Task<Hub?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Hub?>(Denver);
    }

    private sealed class FakeProvider : IHubDataProvider
    {
        public bool Throw { get; init; }
        public int WeatherCalls { get; private set; }
        public int BaselineCalls { get; private set; }

        public DataWindow Window => new(new DateOnly(2021, 1, 1), new DateOnly(2025, 12, 31));

        public Task<IReadOnlyDictionary<string, HubWeatherData>> GetWeatherAsync(
            IReadOnlyCollection<Hub> hubs, CancellationToken cancellationToken = default)
        {
            WeatherCalls++;
            return Task.FromResult<IReadOnlyDictionary<string, HubWeatherData>>(
                hubs.ToDictionary(h => h.Id, h => new HubWeatherData(h.Id, Window, [], DataFreshness.Fresh, DateTimeOffset.UtcNow)));
        }

        public Task<IReadOnlyDictionary<string, HubBaselineData>> GetBaselinesAsync(
            IReadOnlyCollection<Hub> hubs, CancellationToken cancellationToken = default)
        {
            BaselineCalls++;
            if (Throw)
            {
                throw new InvalidOperationException("boom");
            }

            return Task.FromResult<IReadOnlyDictionary<string, HubBaselineData>>(
                hubs.ToDictionary(h => h.CountyFips, h => new HubBaselineData(h.CountyFips, null, DataFreshness.Unavailable, null)));
        }
    }
}
