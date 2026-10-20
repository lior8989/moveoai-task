using System.Globalization;
using System.Net.Http.Json;
using WeatherRisk.Core.Abstractions;
using WeatherRisk.Core.Scoring;

namespace WeatherRisk.Data.External.Fema;

/// <summary>
/// Typed HTTP client for FEMA's National Risk Index county layer (public ArcGIS feature service, no key).
/// Downloads annualized frequencies for all ~3,200 US counties (paged) and returns national percentiles for
/// the requested counties. One fetch serves all hubs; the provider caches the results.
/// </summary>
internal sealed class FemaNriClient : IHazardBaselineSource
{
    /// <summary>Dependency name for metrics/health/logs.</summary>
    public const string DependencyName = DependencyNames.FemaNri;

    /// <summary>Base address of the NRI counties feature layer.</summary>
    public static readonly Uri BaseAddress = new(
        "https://services.arcgis.com/XG15cJAlne2vxtgt/arcgis/rest/services/National_Risk_Index_Counties/FeatureServer/0/");

    /// <summary>Rows per page — the service's documented maximum record count.</summary>
    internal const int PageSize = 2000;

    // ~3,232 counties → 2 pages. The cap stops a misbehaving service from looping forever.
    private const int MaxPages = 10;

    private const string OutFields =
        "STCOFIPS,WNTW_AFREQ,ISTM_AFREQ,CWAV_AFREQ,HRCN_AFREQ,IFLD_AFREQ,CFLD_AFREQ,TRND_AFREQ,HAIL_AFREQ,SWND_AFREQ,HWAV_AFREQ";

    private readonly HttpClient _http;

    /// <summary>Creates the client.</summary>
    /// <param name="http">HttpClient configured by <c>IHttpClientFactory</c> (base address + resilience).</param>
    public FemaNriClient(HttpClient http) => _http = http;

    /// <inheritdoc />
    public string Name => DependencyName;

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, HazardBaseline>> FetchAsync(
        IReadOnlyCollection<string> countyFips, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(countyFips);
        if (countyFips.Count == 0)
        {
            return new Dictionary<string, HazardBaseline>();
        }

        List<(string Fips, double?[] Values)> all = await FetchAllCountiesAsync(cancellationToken);
        return NriPercentileCalculator.Compute(all, countyFips);
    }

    /// <summary>Pages through the layer ordered by FIPS (stable paging) until the service reports no more rows.</summary>
    private async Task<List<(string Fips, double?[] Values)>> FetchAllCountiesAsync(CancellationToken cancellationToken)
    {
        var counties = new List<(string, double?[])>(3300);
        for (int page = 0; page < MaxPages; page++)
        {
            string url = string.Create(CultureInfo.InvariantCulture,
                $"query?where=1%3D1&outFields={OutFields}&returnGeometry=false&orderByFields=STCOFIPS" +
                $"&resultOffset={page * PageSize}&resultRecordCount={PageSize}&f=json");

            using HttpResponseMessage response = await _http.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            NriQueryResponse body = await response.Content.ReadFromJsonAsync(NriJsonContext.Default.NriQueryResponse, cancellationToken)
                ?? throw new InvalidDataException("FEMA NRI returned an empty body.");

            if (body.Error is { } error)
            {
                // ArcGIS signals failures in the body with HTTP 200; surface them as transport-level failures.
                throw new HttpRequestException($"FEMA NRI query failed ({error.Code}): {error.Message}");
            }

            foreach (NriFeature feature in body.Features ?? [])
            {
                if (feature.Attributes is { StateCountyFips: { Length: 5 } fips } attributes)
                {
                    counties.Add((fips, attributes.ToArray()));
                }
            }

            if (!body.ExceededTransferLimit || body.Features is not { Length: > 0 })
            {
                return counties;
            }
        }

        throw new InvalidDataException($"FEMA NRI paging exceeded {MaxPages} pages.");
    }
}
