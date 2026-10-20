using WeatherRisk.Api.Endpoints;
using WeatherRisk.Api.Observability;
using WeatherRisk.Api.Persistence;
using WeatherRisk.Api.RateLimiting;
using WeatherRisk.Data;
using WeatherRisk.Data.Persistence;

// Composition root: service registration and HTTP pipeline. Feature wiring lives in extension methods.
var builder = WebApplication.CreateBuilder(args);

string connectionString = SqliteConnectionStrings.Resolve(
    builder.Configuration.GetConnectionString(SqliteConnectionStrings.Name)
        ?? throw new InvalidOperationException($"Missing connection string '{SqliteConnectionStrings.Name}'."),
    builder.Environment.ContentRootPath);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddObservability(builder.Configuration);
builder.Services.AddApiRateLimiting(builder.Configuration);
builder.Services.AddWeatherRiskPersistence(connectionString);
builder.Services.AddHealthChecks().AddDbContextCheck<WeatherRiskDbContext>("database");

var app = builder.Build();

// Creates/migrates the database (incl. hub seed) before the first request is served.
await app.Services.InitializeWeatherRiskDatabaseAsync();

// Pipeline order matters:
// errors → ProblemDetails, then routing (so metrics know the route template),
// then metrics (so they also see 429s), then the rate limiter, then endpoints.
app.UseExceptionHandler();
app.UseStatusCodePages();

// Swagger is enabled in every environment on purpose: reviewers test the API live.
app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "Weather Risk API v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "Weather Risk API";
});

app.UseRouting();
app.UseRequestMetrics();
app.UseRateLimiter();

app.MapSystemEndpoints();

// Until the chat UI lands (task 8), the root opens Swagger.
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

await app.RunAsync();
