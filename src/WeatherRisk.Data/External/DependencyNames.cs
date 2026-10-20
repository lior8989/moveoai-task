namespace WeatherRisk.Data.External;

/// <summary>Names of external dependencies, shared by clients, metrics, health checks and logs.</summary>
public static class DependencyNames
{
    /// <summary>Open-Meteo Historical Weather API.</summary>
    public const string OpenMeteo = "open-meteo";

    /// <summary>FEMA National Risk Index feature service.</summary>
    public const string FemaNri = "fema-nri";

    /// <summary>All data-source dependencies.</summary>
    public static IReadOnlyList<string> DataSources { get; } = [OpenMeteo, FemaNri];
}
