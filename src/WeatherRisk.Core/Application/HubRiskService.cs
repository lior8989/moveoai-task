using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Ranking;
using WeatherRisk.Core.Scoring;
using WeatherRisk.Core.Weather;

namespace WeatherRisk.Core.Application;

/// <summary>
/// Application service (use cases) behind both the REST API and the LLM agent's tools: assess, rank and
/// compare hubs, and compute weather statistics. Orchestrates data loading → aggregation → deterministic
/// scoring; it contains no scoring math itself.
/// </summary>
/// <remarks>
/// Scoped (its data provider uses a scoped DbContext). Scores are computed per call: with a warm cache,
/// all 15 hubs load and score in well under 100 ms, so an extra result cache would add invalidation
/// complexity without a measurable benefit.
/// </remarks>
public sealed class HubRiskService
{
    /// <summary>Maximum hubs in one comparison.</summary>
    public const int MaxCompareHubs = 15;

    private readonly IHubRepository _hubs;
    private readonly IHubDataProvider _data;
    private readonly WeatherStatisticsCalculator _calculator;
    private readonly RiskScoringEngine _engine;
    private readonly WeatherThresholds _thresholds;

    /// <summary>Creates the service.</summary>
    /// <param name="hubs">Hub catalog.</param>
    /// <param name="data">Cache-first data provider.</param>
    /// <param name="calculator">Weather statistics calculator.</param>
    /// <param name="engine">Deterministic risk model.</param>
    /// <param name="thresholds">Disruption-day thresholds (reported with statistics).</param>
    public HubRiskService(
        IHubRepository hubs,
        IHubDataProvider data,
        WeatherStatisticsCalculator calculator,
        RiskScoringEngine engine,
        WeatherThresholds thresholds)
    {
        _hubs = hubs;
        _data = data;
        _calculator = calculator;
        _engine = engine;
        _thresholds = thresholds;
    }

    /// <summary>The observed-weather window currently used for scoring.</summary>
    public DataWindow Window => _data.Window;

    /// <summary>Lists hubs, optionally only one region.</summary>
    /// <param name="region">Region filter; null = all.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Hubs ordered by name.</returns>
    public async Task<IReadOnlyList<Hub>> GetHubsAsync(Region? region = null, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Hub> all = await _hubs.GetAllAsync(cancellationToken);
        return region is { } r ? all.Where(h => h.Region == r).ToList() : all;
    }

    /// <summary>Returns one hub.</summary>
    /// <param name="hubId">Hub id (case-insensitive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The hub.</returns>
    /// <exception cref="HubNotFoundException">When the hub doesn't exist.</exception>
    public async Task<Hub> GetHubAsync(string hubId, CancellationToken cancellationToken = default) =>
        await _hubs.GetByIdAsync(hubId, cancellationToken) ?? throw new HubNotFoundException([hubId]);

    /// <summary>Scores one hub with its full breakdown and data provenance.</summary>
    /// <param name="hubId">Hub id (case-insensitive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The assessment.</returns>
    /// <exception cref="HubNotFoundException">When the hub doesn't exist.</exception>
    public async Task<HubAssessment> AssessAsync(string hubId, CancellationToken cancellationToken = default)
    {
        Hub hub = await GetHubAsync(hubId, cancellationToken);
        return (await AssessAsync([hub], cancellationToken))[0];
    }

    /// <summary>Scores several hubs, loading their data in one batch.</summary>
    /// <param name="hubs">Hubs to score.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Assessments in input order.</returns>
    public async Task<IReadOnlyList<HubAssessment>> AssessAsync(
        IReadOnlyCollection<Hub> hubs, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hubs);
        if (hubs.Count == 0)
        {
            return [];
        }

        // Baselines first: one call covers all counties; weather then loads per hub with bounded parallelism.
        IReadOnlyDictionary<string, HubBaselineData> baselines = await _data.GetBaselinesAsync(hubs, cancellationToken);
        IReadOnlyDictionary<string, HubWeatherData> weather = await _data.GetWeatherAsync(hubs, cancellationToken);
        DataWindow window = _data.Window;

        return hubs.Select(hub => Assess(hub, weather[hub.Id], baselines[hub.CountyFips], window)).ToList();
    }

    /// <summary>Ranks hubs by composite score or by one hazard.</summary>
    /// <param name="criteria">Region filter, hazard and top-N.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ranking with provenance.</returns>
    /// <exception cref="RequestValidationException">When <see cref="RankingCriteria.Top"/> is not positive.</exception>
    public async Task<RankingResult> RankAsync(RankingCriteria criteria, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        if (criteria.Top is <= 0)
        {
            throw new RequestValidationException("'top' must be a positive number.");
        }

        // Only load and score the hubs that can appear in the result.
        IReadOnlyList<Hub> hubs = await GetHubsAsync(criteria.Region, cancellationToken);
        IReadOnlyList<HubAssessment> assessments = await AssessAsync(hubs, cancellationToken);
        IReadOnlyList<RankedHub> ranked = HubRanker.Rank(assessments.Select(a => a.Score), criteria);
        return new RankingResult(criteria, ranked, ProvenanceById(assessments));
    }

    /// <summary>Compares two or more hubs side by side.</summary>
    /// <param name="hubIds">Hub ids (case-insensitive, distinct).</param>
    /// <param name="hazards">Hazards to compare; null/empty = all.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Comparison with provenance.</returns>
    /// <exception cref="RequestValidationException">When fewer than 2, more than <see cref="MaxCompareHubs"/>, or duplicate hubs are given.</exception>
    /// <exception cref="HubNotFoundException">When any hub doesn't exist.</exception>
    public async Task<ComparisonResult> CompareAsync(
        IReadOnlyList<string> hubIds, IReadOnlyCollection<HazardType>? hazards = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hubIds);
        List<string> ids = hubIds.Select(Hub.NormalizeId).Where(id => id.Length > 0).ToList();
        if (ids.Count is < 2 or > MaxCompareHubs)
        {
            throw new RequestValidationException($"Compare between 2 and {MaxCompareHubs} hubs (got {ids.Count}).");
        }

        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
        {
            throw new RequestValidationException("Each hub may appear only once in a comparison.");
        }

        // One catalog read instead of one query per id.
        Dictionary<string, Hub> catalog = (await _hubs.GetAllAsync(cancellationToken)).ToDictionary(h => h.Id, StringComparer.Ordinal);
        List<string> unknown = ids.Where(id => !catalog.ContainsKey(id)).ToList();
        if (unknown.Count > 0)
        {
            throw new HubNotFoundException(unknown);
        }

        IReadOnlyList<HubAssessment> assessments = await AssessAsync(ids.Select(id => catalog[id]).ToList(), cancellationToken);
        HubComparison comparison = HubRanker.Compare(assessments.Select(a => a.Score).ToList(), hazards);
        return new ComparisonResult(comparison, ProvenanceById(assessments));
    }

    /// <summary>
    /// Weather statistics (disruption-day counts, percentages, extremes) for a hub over any period inside the
    /// data window. Defaults to the last full calendar year.
    /// </summary>
    /// <param name="hubId">Hub id (case-insensitive).</param>
    /// <param name="from">First day; null = 1 Jan of the last full year.</param>
    /// <param name="to">Last day; null = 31 Dec of the last full year.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Statistics with thresholds and provenance.</returns>
    /// <exception cref="HubNotFoundException">When the hub doesn't exist.</exception>
    /// <exception cref="RequestValidationException">When the period is reversed or outside the data window.</exception>
    public async Task<WeatherStatisticsResult> GetWeatherStatisticsAsync(
        string hubId, DateOnly? from = null, DateOnly? to = null, CancellationToken cancellationToken = default)
    {
        DataWindow window = _data.Window;
        DateOnly start = from ?? new DateOnly(window.To.Year, 1, 1);
        DateOnly end = to ?? new DateOnly(window.To.Year, 12, 31);
        if (end < start)
        {
            throw new RequestValidationException($"'to' ({end:yyyy-MM-dd}) is before 'from' ({start:yyyy-MM-dd}).");
        }

        if (start < window.From || end > window.To)
        {
            throw new RequestValidationException(
                $"Weather statistics are available for {window.From:yyyy-MM-dd} to {window.To:yyyy-MM-dd} " +
                "(the last full calendar years in the data window).");
        }

        Hub hub = await GetHubAsync(hubId, cancellationToken);
        HubWeatherData data = (await _data.GetWeatherAsync([hub], cancellationToken))[hub.Id];
        WeatherSummary summary = _calculator.Summarize(data.Days, start, end);
        return new WeatherStatisticsResult(hub, summary, _thresholds, data.Freshness, data.RetrievedAt);
    }

    /// <summary>Scores one hub and appends notes about stale data (missing data is noted by the engine).</summary>
    private HubAssessment Assess(Hub hub, HubWeatherData weather, HubBaselineData baseline, DataWindow window)
    {
        WeatherSummary? summary = weather.Freshness == DataFreshness.Unavailable
            ? null
            : _calculator.Summarize(weather.Days, window.From, window.To);

        HubRiskScore score = _engine.Score(new HubHazardInputs(hub, summary, baseline.Baseline));

        var notes = new List<string>(score.Notes);
        if (weather.Freshness == DataFreshness.Stale)
        {
            notes.Add($"Weather source unavailable — using cached weather retrieved {weather.RetrievedAt:yyyy-MM-dd}.");
        }

        if (baseline.Freshness == DataFreshness.Stale)
        {
            notes.Add($"FEMA source unavailable — using cached baseline retrieved {baseline.RetrievedAt:yyyy-MM-dd}.");
        }

        var provenance = new HubDataProvenance(
            window, weather.Freshness, weather.RetrievedAt, baseline.Freshness, baseline.RetrievedAt);
        return new HubAssessment(notes.Count == score.Notes.Count ? score : score with { Notes = notes }, provenance);
    }

    private static Dictionary<string, HubDataProvenance> ProvenanceById(IReadOnlyList<HubAssessment> assessments) =>
        assessments.ToDictionary(a => a.Score.Hub.Id, a => a.Provenance, StringComparer.Ordinal);
}
