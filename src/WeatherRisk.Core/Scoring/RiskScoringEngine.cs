using WeatherRisk.Core.Domain;
using WeatherRisk.Core.Scoring.Hazards;

namespace WeatherRisk.Core.Scoring;

/// <summary>
/// Deterministic risk model: runs every hazard scorer and combines them into an explainable composite.
/// <code>composite = b × worstHazard + (1 − b) × Σ(wᵢ × scoreᵢ) / Σwᵢ</code>
/// where b = <see cref="ScoringSettings.WorstHazardBlend"/>.
/// </summary>
/// <remarks>Stateless after construction and thread-safe (register as a singleton).</remarks>
public sealed class RiskScoringEngine
{
    private const int MaxTopDrivers = 2;

    private readonly IHazardScorer[] _scorers;
    private readonly double[] _normalizedWeights;
    private readonly ScoringSettings _settings;

    /// <summary>Creates the engine.</summary>
    /// <param name="scorers">Exactly one scorer per <see cref="HazardType"/>.</param>
    /// <param name="settings">Model settings; validated here so misconfiguration fails at startup.</param>
    /// <exception cref="ArgumentException">When settings are invalid or a hazard has zero or several scorers.</exception>
    public RiskScoringEngine(IEnumerable<IHazardScorer> scorers, ScoringSettings settings)
    {
        ArgumentNullException.ThrowIfNull(scorers);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        _settings = settings;

        HazardType[] hazards = Enum.GetValues<HazardType>();
        var byHazard = new Dictionary<HazardType, IHazardScorer>();
        foreach (IHazardScorer scorer in scorers)
        {
            if (!byHazard.TryAdd(scorer.Hazard, scorer))
            {
                throw new ArgumentException($"More than one scorer registered for {scorer.Hazard}.", nameof(scorers));
            }
        }

        // Fixed HazardType order: stable output and index-aligned with the precomputed weights.
        _scorers = hazards
            .Select(h => byHazard.TryGetValue(h, out IHazardScorer? s)
                ? s
                : throw new ArgumentException($"No scorer registered for {h}.", nameof(scorers)))
            .ToArray();

        double totalWeight = hazards.Sum(h => settings.For(h).CompositeWeight);
        _normalizedWeights = hazards.Select(h => settings.For(h).CompositeWeight / totalWeight).ToArray();
    }

    /// <summary>Scores one hub.</summary>
    /// <param name="inputs">Hub plus its weather summary and baseline (either may be null).</param>
    /// <returns>Composite score, rating, per-hazard breakdown, drivers, confidence and data notes.</returns>
    public HubRiskScore Score(HubHazardInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var scores = new HazardScore[_scorers.Length];
        int worstIndex = 0;
        double weightedAverage = 0;
        for (int i = 0; i < _scorers.Length; i++)
        {
            scores[i] = _scorers[i].Score(inputs.Weather, inputs.Baseline);
            weightedAverage += _normalizedWeights[i] * scores[i].Score;
            if (scores[i].Score > scores[worstIndex].Score)
            {
                worstIndex = i; // Ties keep the first hazard in enum order — deterministic.
            }
        }

        double blend = _settings.WorstHazardBlend;
        double composite = (blend * scores[worstIndex].Score) + ((1 - blend) * weightedAverage);

        // Attribution: each hazard gets its share of the weighted average; the worst hazard also gets the
        // worst-hazard part. By construction the contributions sum exactly to the composite.
        var assessments = new HazardAssessment[scores.Length];
        for (int i = 0; i < scores.Length; i++)
        {
            double contribution = (1 - blend) * _normalizedWeights[i] * scores[i].Score;
            if (i == worstIndex)
            {
                contribution += blend * scores[i].Score;
            }

            assessments[i] = new HazardAssessment(
                scores[i], HazardMath.Round(_normalizedWeights[i], 3), HazardMath.Round(contribution, 2), i == worstIndex);
        }

        double roundedComposite = HazardMath.Round(composite);
        (ScoreConfidence confidence, List<string> notes) = AssessData(inputs);

        return new HubRiskScore(
            inputs.Hub,
            roundedComposite,
            RiskRatings.FromScore(roundedComposite),
            confidence,
            assessments,
            TopDrivers(assessments),
            notes,
            inputs.Weather?.From,
            inputs.Weather?.To);
    }

    /// <summary>Hazards with a positive contribution, largest first, at most <see cref="MaxTopDrivers"/>.</summary>
    private static HazardType[] TopDrivers(HazardAssessment[] assessments) => assessments
        .Where(a => a.Contribution > 0)
        .OrderByDescending(a => a.Contribution)
        .Take(MaxTopDrivers)
        .Select(a => a.Detail.Hazard)
        .ToArray();

    /// <summary>Derives confidence from which signals are present and complete, with human-readable caveats.</summary>
    private (ScoreConfidence Confidence, List<string> Notes) AssessData(HubHazardInputs inputs)
    {
        var notes = new List<string>(2);

        bool weatherComplete = false;
        if (inputs.Weather is not { DaysObserved: > 0 } weather)
        {
            notes.Add("Observed weather unavailable — hazard scores use the FEMA NRI baseline only.");
        }
        else if (weather.Coverage < _settings.MinimumCoverage)
        {
            notes.Add($"Weather data covers only {weather.Coverage:P0} of {weather.From:yyyy-MM-dd}..{weather.To:yyyy-MM-dd}.");
        }
        else
        {
            weatherComplete = true;
        }

        bool baselinePresent = inputs.Baseline is not null;
        if (!baselinePresent)
        {
            notes.Add("FEMA NRI baseline unavailable — hazard scores use observed weather only.");
        }

        ScoreConfidence confidence = (weatherComplete, baselinePresent) switch
        {
            (true, true) => ScoreConfidence.High,
            (false, false) => ScoreConfidence.Low,
            _ => ScoreConfidence.Medium,
        };

        return (confidence, notes);
    }
}
