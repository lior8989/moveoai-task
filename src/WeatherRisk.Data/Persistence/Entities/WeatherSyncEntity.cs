namespace WeatherRisk.Data.Persistence.Entities;

/// <summary>Row of the <c>WeatherSync</c> table: which period of weather is cached for a hub and when it was fetched.</summary>
public sealed class WeatherSyncEntity
{
    /// <summary>Hub id (primary key, FK to <c>Hubs</c>).</summary>
    public required string HubId { get; set; }

    /// <summary>First cached day.</summary>
    public DateOnly CoveredFrom { get; set; }

    /// <summary>Last cached day.</summary>
    public DateOnly CoveredTo { get; set; }

    /// <summary>When the data was fetched from the source.</summary>
    public DateTimeOffset SyncedAt { get; set; }
}
