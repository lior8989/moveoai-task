using System.ComponentModel.DataAnnotations;

namespace WeatherRisk.Api.RateLimiting;

/// <summary>Rate-limiting configuration, bound from the <c>RateLimiting</c> configuration section.</summary>
public sealed class RateLimitingSettings
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "RateLimiting";

    /// <summary>Per-client limit applied to all <c>/api</c> endpoints except <c>/api/system/*</c>.</summary>
    [Required]
    public SlidingWindowSettings Global { get; init; } = new();

    /// <summary>Per-client limit for <c>/api/chat</c> (default 10 per minute) — each question triggers several LLM calls.</summary>
    [Required]
    public SlidingWindowSettings Chat { get; init; } = new() { PermitLimit = 10, WindowSeconds = 60, SegmentsPerWindow = 6 };
}

/// <summary>Parameters of a sliding-window limiter: at most <see cref="PermitLimit"/> requests per window.</summary>
public sealed class SlidingWindowSettings
{
    /// <summary>Maximum requests allowed per client within the window.</summary>
    [Range(1, 100_000)]
    public int PermitLimit { get; init; } = 120;

    /// <summary>Window length in seconds.</summary>
    [Range(1, 3600)]
    public int WindowSeconds { get; init; } = 60;

    /// <summary>
    /// Number of segments the window slides by. More segments = smoother limiting, slightly more memory.
    /// </summary>
    [Range(1, 600)]
    public int SegmentsPerWindow { get; init; } = 6;

    /// <summary>Duration of one segment — the soonest a rejected client can expect capacity back.</summary>
    public TimeSpan SegmentDuration => TimeSpan.FromSeconds((double)WindowSeconds / SegmentsPerWindow);
}
