using System.ComponentModel.DataAnnotations;

namespace WeatherRisk.Data.External;

/// <summary>Cache and fetch behaviour of the hub data provider, bound from the <c>HubData</c> configuration section.</summary>
public sealed class HubDataSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "HubData";

    /// <summary>Observed-weather look-back in full calendar years (default 5 → e.g. 2021–2025 when run in 2026).</summary>
    [Range(1, 30)]
    public int LookbackYears { get; init; } = 5;

    /// <summary>
    /// Days a cached weather window stays valid (default 30). Completed years rarely change, so this mainly
    /// picks up late corrections; a new calendar year triggers a refetch regardless (the window moves).
    /// </summary>
    [Range(1, 365)]
    public int WeatherCacheDays { get; init; } = 30;

    /// <summary>Days cached FEMA baselines stay valid (default 30; NRI is republished roughly yearly).</summary>
    [Range(1, 365)]
    public int BaselineCacheDays { get; init; } = 30;

    /// <summary>Maximum concurrent weather fetches (default 4) — fast warm-up without hammering the free API.</summary>
    [Range(1, 16)]
    public int MaxConcurrentFetches { get; init; } = 4;

    /// <summary>
    /// Fill the cache for all hubs in the background when the app starts (default true), so the first user
    /// question is answered from cache instead of waiting on a cold multi-year fetch.
    /// </summary>
    public bool WarmUpOnStartup { get; init; } = true;
}
