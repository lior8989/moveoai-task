using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WeatherRisk.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BaselineFrequencyPercentiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "HazardBaselines",
                comment: "Cached FEMA National Risk Index baselines per county: hazard frequency percentiles vs all US counties.",
                oldComment: "Cached FEMA National Risk Index hazard scores per county.");

            migrationBuilder.AlterColumn<double>(
                name: "WinterWeather",
                table: "HazardBaselines",
                type: "REAL",
                nullable: true,
                comment: "NRI Winter Weather annualized frequency as national percentile among US counties, 0-100 (null = not applicable).",
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true,
                oldComment: "NRI Winter Weather risk score: national percentile 0-100 (null = not rated).");

            migrationBuilder.AlterColumn<double>(
                name: "Tornado",
                table: "HazardBaselines",
                type: "REAL",
                nullable: true,
                comment: "NRI Tornado annualized frequency as national percentile among US counties, 0-100 (null = not applicable).",
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true,
                oldComment: "NRI Tornado risk score: national percentile 0-100 (null = not rated).");

            migrationBuilder.AlterColumn<double>(
                name: "StrongWind",
                table: "HazardBaselines",
                type: "REAL",
                nullable: true,
                comment: "NRI Strong Wind annualized frequency as national percentile among US counties, 0-100 (null = not applicable).",
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true,
                oldComment: "NRI Strong Wind risk score: national percentile 0-100 (null = not rated).");

            migrationBuilder.AlterColumn<double>(
                name: "RiverineFlooding",
                table: "HazardBaselines",
                type: "REAL",
                nullable: true,
                comment: "NRI Inland (riverine) Flooding annualized frequency as national percentile among US counties, 0-100 (null = not applicable).",
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true,
                oldComment: "NRI Riverine Flooding risk score: national percentile 0-100 (null = not rated).");

            migrationBuilder.AlterColumn<double>(
                name: "IceStorm",
                table: "HazardBaselines",
                type: "REAL",
                nullable: true,
                comment: "NRI Ice Storm annualized frequency as national percentile among US counties, 0-100 (null = not applicable).",
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true,
                oldComment: "NRI Ice Storm risk score: national percentile 0-100 (null = not rated).");

            migrationBuilder.AlterColumn<double>(
                name: "Hurricane",
                table: "HazardBaselines",
                type: "REAL",
                nullable: true,
                comment: "NRI Hurricane annualized frequency as national percentile among US counties, 0-100 (null = not applicable).",
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true,
                oldComment: "NRI Hurricane risk score: national percentile 0-100 (null = not rated).");

            migrationBuilder.AlterColumn<double>(
                name: "HeatWave",
                table: "HazardBaselines",
                type: "REAL",
                nullable: true,
                comment: "NRI Heat Wave annualized frequency as national percentile among US counties, 0-100 (null = not applicable).",
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true,
                oldComment: "NRI Heat Wave risk score: national percentile 0-100 (null = not rated).");

            migrationBuilder.AlterColumn<double>(
                name: "Hail",
                table: "HazardBaselines",
                type: "REAL",
                nullable: true,
                comment: "NRI Hail annualized frequency as national percentile among US counties, 0-100 (null = not applicable).",
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true,
                oldComment: "NRI Hail risk score: national percentile 0-100 (null = not rated).");

            migrationBuilder.AlterColumn<double>(
                name: "ColdWave",
                table: "HazardBaselines",
                type: "REAL",
                nullable: true,
                comment: "NRI Cold Wave annualized frequency as national percentile among US counties, 0-100 (null = not applicable).",
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true,
                oldComment: "NRI Cold Wave risk score: national percentile 0-100 (null = not rated).");

            migrationBuilder.AlterColumn<double>(
                name: "CoastalFlooding",
                table: "HazardBaselines",
                type: "REAL",
                nullable: true,
                comment: "NRI Coastal Flooding annualized frequency as national percentile among US counties, 0-100 (null = not applicable).",
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true,
                oldComment: "NRI Coastal Flooding risk score: national percentile 0-100 (null = not rated).");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterTable(
                name: "HazardBaselines",
                comment: "Cached FEMA National Risk Index hazard scores per county.",
                oldComment: "Cached FEMA National Risk Index baselines per county: hazard frequency percentiles vs all US counties.");

            migrationBuilder.AlterColumn<double>(
                name: "WinterWeather",
                table: "HazardBaselines",
                type: "REAL",
                nullable: true,
                comment: "NRI Winter Weather risk score: national percentile 0-100 (null = not rated).",
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true,
                oldComment: "NRI Winter Weather annualized frequency as national percentile among US counties, 0-100 (null = not applicable).");

            migrationBuilder.AlterColumn<double>(
                name: "Tornado",
                table: "HazardBaselines",
                type: "REAL",
                nullable: true,
                comment: "NRI Tornado risk score: national percentile 0-100 (null = not rated).",
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true,
                oldComment: "NRI Tornado annualized frequency as national percentile among US counties, 0-100 (null = not applicable).");

            migrationBuilder.AlterColumn<double>(
                name: "StrongWind",
                table: "HazardBaselines",
                type: "REAL",
                nullable: true,
                comment: "NRI Strong Wind risk score: national percentile 0-100 (null = not rated).",
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true,
                oldComment: "NRI Strong Wind annualized frequency as national percentile among US counties, 0-100 (null = not applicable).");

            migrationBuilder.AlterColumn<double>(
                name: "RiverineFlooding",
                table: "HazardBaselines",
                type: "REAL",
                nullable: true,
                comment: "NRI Riverine Flooding risk score: national percentile 0-100 (null = not rated).",
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true,
                oldComment: "NRI Inland (riverine) Flooding annualized frequency as national percentile among US counties, 0-100 (null = not applicable).");

            migrationBuilder.AlterColumn<double>(
                name: "IceStorm",
                table: "HazardBaselines",
                type: "REAL",
                nullable: true,
                comment: "NRI Ice Storm risk score: national percentile 0-100 (null = not rated).",
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true,
                oldComment: "NRI Ice Storm annualized frequency as national percentile among US counties, 0-100 (null = not applicable).");

            migrationBuilder.AlterColumn<double>(
                name: "Hurricane",
                table: "HazardBaselines",
                type: "REAL",
                nullable: true,
                comment: "NRI Hurricane risk score: national percentile 0-100 (null = not rated).",
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true,
                oldComment: "NRI Hurricane annualized frequency as national percentile among US counties, 0-100 (null = not applicable).");

            migrationBuilder.AlterColumn<double>(
                name: "HeatWave",
                table: "HazardBaselines",
                type: "REAL",
                nullable: true,
                comment: "NRI Heat Wave risk score: national percentile 0-100 (null = not rated).",
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true,
                oldComment: "NRI Heat Wave annualized frequency as national percentile among US counties, 0-100 (null = not applicable).");

            migrationBuilder.AlterColumn<double>(
                name: "Hail",
                table: "HazardBaselines",
                type: "REAL",
                nullable: true,
                comment: "NRI Hail risk score: national percentile 0-100 (null = not rated).",
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true,
                oldComment: "NRI Hail annualized frequency as national percentile among US counties, 0-100 (null = not applicable).");

            migrationBuilder.AlterColumn<double>(
                name: "ColdWave",
                table: "HazardBaselines",
                type: "REAL",
                nullable: true,
                comment: "NRI Cold Wave risk score: national percentile 0-100 (null = not rated).",
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true,
                oldComment: "NRI Cold Wave annualized frequency as national percentile among US counties, 0-100 (null = not applicable).");

            migrationBuilder.AlterColumn<double>(
                name: "CoastalFlooding",
                table: "HazardBaselines",
                type: "REAL",
                nullable: true,
                comment: "NRI Coastal Flooding risk score: national percentile 0-100 (null = not rated).",
                oldClrType: typeof(double),
                oldType: "REAL",
                oldNullable: true,
                oldComment: "NRI Coastal Flooding annualized frequency as national percentile among US counties, 0-100 (null = not applicable).");
        }
    }
}
