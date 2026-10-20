using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WeatherRisk.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HazardBaselines",
                columns: table => new
                {
                    CountyFips = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false, comment: "5-digit county FIPS code (PK)."),
                    WinterWeather = table.Column<double>(type: "REAL", nullable: true, comment: "NRI Winter Weather risk score: national percentile 0-100 (null = not rated)."),
                    IceStorm = table.Column<double>(type: "REAL", nullable: true, comment: "NRI Ice Storm risk score: national percentile 0-100 (null = not rated)."),
                    ColdWave = table.Column<double>(type: "REAL", nullable: true, comment: "NRI Cold Wave risk score: national percentile 0-100 (null = not rated)."),
                    Hurricane = table.Column<double>(type: "REAL", nullable: true, comment: "NRI Hurricane risk score: national percentile 0-100 (null = not rated)."),
                    RiverineFlooding = table.Column<double>(type: "REAL", nullable: true, comment: "NRI Riverine Flooding risk score: national percentile 0-100 (null = not rated)."),
                    CoastalFlooding = table.Column<double>(type: "REAL", nullable: true, comment: "NRI Coastal Flooding risk score: national percentile 0-100 (null = not rated)."),
                    Tornado = table.Column<double>(type: "REAL", nullable: true, comment: "NRI Tornado risk score: national percentile 0-100 (null = not rated)."),
                    Hail = table.Column<double>(type: "REAL", nullable: true, comment: "NRI Hail risk score: national percentile 0-100 (null = not rated)."),
                    StrongWind = table.Column<double>(type: "REAL", nullable: true, comment: "NRI Strong Wind risk score: national percentile 0-100 (null = not rated)."),
                    HeatWave = table.Column<double>(type: "REAL", nullable: true, comment: "NRI Heat Wave risk score: national percentile 0-100 (null = not rated)."),
                    FetchedAt = table.Column<long>(type: "INTEGER", nullable: false, comment: "When the scores were fetched from FEMA (UTC, stored as 64-bit integer).")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HazardBaselines", x => x.CountyFips);
                },
                comment: "Cached FEMA National Risk Index hazard scores per county.");

            migrationBuilder.CreateTable(
                name: "Hubs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false, comment: "Lower-case hub id used in APIs, e.g. 'dallas'."),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false, comment: "Display name."),
                    City = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false, comment: "City."),
                    State = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false, comment: "Two-letter US state code."),
                    Region = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false, comment: "US Census region: Midwest, South, West, Northeast."),
                    Latitude = table.Column<double>(type: "REAL", nullable: false, comment: "Latitude, decimal degrees (WGS84)."),
                    Longitude = table.Column<double>(type: "REAL", nullable: false, comment: "Longitude, decimal degrees (WGS84)."),
                    CountyFips = table.Column<string>(type: "TEXT", maxLength: 5, nullable: false, comment: "5-digit county FIPS code (FEMA NRI lookup key).")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hubs", x => x.Id);
                },
                comment: "Distribution hubs assessed for weather risk (reference data).");

            migrationBuilder.CreateTable(
                name: "DailyWeather",
                columns: table => new
                {
                    HubId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false, comment: "Hub id (FK to Hubs)."),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false, comment: "Calendar date (hub local)."),
                    SnowfallCm = table.Column<double>(type: "REAL", nullable: true, comment: "Daily snowfall, cm."),
                    PrecipitationMm = table.Column<double>(type: "REAL", nullable: true, comment: "Daily precipitation, mm."),
                    MaxWindGustKmh = table.Column<double>(type: "REAL", nullable: true, comment: "Daily maximum wind gust, km/h."),
                    MaxTemperatureC = table.Column<double>(type: "REAL", nullable: true, comment: "Daily maximum temperature at 2 m, °C."),
                    MinTemperatureC = table.Column<double>(type: "REAL", nullable: true, comment: "Daily minimum temperature at 2 m, °C.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyWeather", x => new { x.HubId, x.Date });
                    table.ForeignKey(
                        name: "FK_DailyWeather_Hubs_HubId",
                        column: x => x.HubId,
                        principalTable: "Hubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Cached daily weather per hub (from Open-Meteo historical API).");

            migrationBuilder.CreateTable(
                name: "RiskScoreSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false, comment: "Auto-increment id; also the chronological order of snapshots.")
                        .Annotation("Sqlite:Autoincrement", true),
                    HubId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false, comment: "Hub id (FK to Hubs)."),
                    ComputedAt = table.Column<long>(type: "INTEGER", nullable: false, comment: "When the score was computed (UTC, stored as 64-bit integer)."),
                    CompositeScore = table.Column<double>(type: "REAL", nullable: false, comment: "Composite risk score 0-100."),
                    Rating = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false, comment: "Composite rating: Low, Moderate, High, VeryHigh."),
                    Confidence = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false, comment: "Data confidence: Low, Medium, High."),
                    WinterScore = table.Column<double>(type: "REAL", nullable: false, comment: "Winter hazard score 0-100."),
                    HurricaneScore = table.Column<double>(type: "REAL", nullable: false, comment: "Hurricane hazard score 0-100."),
                    FloodScore = table.Column<double>(type: "REAL", nullable: false, comment: "Flood hazard score 0-100."),
                    SevereStormScore = table.Column<double>(type: "REAL", nullable: false, comment: "Severe-storm hazard score 0-100."),
                    HeatScore = table.Column<double>(type: "REAL", nullable: false, comment: "Heat hazard score 0-100.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskScoreSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskScoreSnapshots_Hubs_HubId",
                        column: x => x.HubId,
                        principalTable: "Hubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "History of computed hub risk scores (trends and change alerts).");

            migrationBuilder.CreateTable(
                name: "WeatherSync",
                columns: table => new
                {
                    HubId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false, comment: "Hub id (PK, FK to Hubs)."),
                    CoveredFrom = table.Column<DateOnly>(type: "TEXT", nullable: false, comment: "First day of the last refreshed period."),
                    CoveredTo = table.Column<DateOnly>(type: "TEXT", nullable: false, comment: "Last day of the last refreshed period."),
                    SyncedAt = table.Column<long>(type: "INTEGER", nullable: false, comment: "When the period was fetched from Open-Meteo (UTC, stored as 64-bit integer).")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeatherSync", x => x.HubId);
                    table.ForeignKey(
                        name: "FK_WeatherSync_Hubs_HubId",
                        column: x => x.HubId,
                        principalTable: "Hubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                },
                comment: "Which weather period is cached per hub and when it was fetched (cache freshness).");

            migrationBuilder.InsertData(
                table: "Hubs",
                columns: new[] { "Id", "City", "CountyFips", "Latitude", "Longitude", "Name", "Region", "State" },
                values: new object[,]
                {
                    { "atlanta", "Atlanta", "13121", 33.749000000000002, -84.388000000000005, "Atlanta Hub", "South", "GA" },
                    { "chicago", "Chicago", "17031", 41.878100000000003, -87.629800000000003, "Chicago Hub", "Midwest", "IL" },
                    { "dallas", "Dallas", "48113", 32.776699999999998, -96.796999999999997, "Dallas Hub", "South", "TX" },
                    { "denver", "Denver", "08031", 39.739199999999997, -104.9903, "Denver Hub", "West", "CO" },
                    { "detroit", "Detroit", "26163", 42.331400000000002, -83.0458, "Detroit Hub", "Midwest", "MI" },
                    { "houston", "Houston", "48201", 29.760400000000001, -95.369799999999998, "Houston Hub", "South", "TX" },
                    { "indianapolis", "Indianapolis", "18097", 39.7684, -86.158100000000005, "Indianapolis Hub", "Midwest", "IN" },
                    { "kansas-city", "Kansas City", "29095", 39.099699999999999, -94.578599999999994, "Kansas City Hub", "Midwest", "MO" },
                    { "los-angeles", "Los Angeles", "06037", 34.052199999999999, -118.2437, "Los Angeles Hub", "West", "CA" },
                    { "miami", "Miami", "12086", 25.761700000000001, -80.191800000000001, "Miami Hub", "South", "FL" },
                    { "minneapolis", "Minneapolis", "27053", 44.977800000000002, -93.265000000000001, "Minneapolis Hub", "Midwest", "MN" },
                    { "new-orleans", "New Orleans", "22071", 29.9511, -90.0715, "New Orleans Hub", "South", "LA" },
                    { "new-york", "New York", "36061", 40.712800000000001, -74.006, "New York Hub", "Northeast", "NY" },
                    { "phoenix", "Phoenix", "04013", 33.448399999999999, -112.074, "Phoenix Hub", "West", "AZ" },
                    { "seattle", "Seattle", "53033", 47.606200000000001, -122.3321, "Seattle Hub", "West", "WA" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hubs_Region",
                table: "Hubs",
                column: "Region");

            migrationBuilder.CreateIndex(
                name: "IX_RiskScoreSnapshots_HubId_Id",
                table: "RiskScoreSnapshots",
                columns: new[] { "HubId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyWeather");

            migrationBuilder.DropTable(
                name: "HazardBaselines");

            migrationBuilder.DropTable(
                name: "RiskScoreSnapshots");

            migrationBuilder.DropTable(
                name: "WeatherSync");

            migrationBuilder.DropTable(
                name: "Hubs");
        }
    }
}
