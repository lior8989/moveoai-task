using System.Text.Json.Serialization;
using WeatherRisk.Api.Agent;
using WeatherRisk.Api.Background;
using WeatherRisk.Api.Endpoints;
using WeatherRisk.Api.Errors;
using WeatherRisk.Api.Observability;
using WeatherRisk.Api.Persistence;
using WeatherRisk.Api.RateLimiting;
using WeatherRisk.Api.Risk;
using WeatherRisk.Data;
using WeatherRisk.Data.External;
using WeatherRisk.Data.Persistence;

// Composition root: service registration and HTTP pipeline. Feature wiring lives in extension methods.
var builder = WebApplication.CreateBuilder(args);

string connectionString = SqliteConnectionStrings.Resolve(
    builder.Configuration.GetConnectionString(SqliteConnectionStrings.Name)
        ?? throw new InvalidOperationException($"Missing connection string '{SqliteConnectionStrings.Name}'."),
    builder.Environment.ContentRootPath);

// Enums travel as names ("Winter", "High") — readable for analysts and the LLM, and listed in Swagger.
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddObservability(builder.Configuration);
builder.Services.AddApiRateLimiting(builder.Configuration);
builder.Services.AddWeatherRiskPersistence(connectionString);
builder.Services.AddWeatherRiskDataSources(
    builder.Configuration.GetSection(HubDataSettings.SectionName),
    (dependency, client) => client.AddDependencyMetrics(dependency));
builder.Services.AddRiskModel(builder.Configuration);
IReadOnlyList<string> llmDependencies = builder.Services.AddChatAgent(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<WeatherRiskDbContext>("database")
    .AddDependencyChecks(DependencyNames.DataSources)
    .AddDependencyChecks(llmDependencies);
builder.Services.AddHostedService<DataWarmupService>();

var app = builder.Build();

// Creates/migrates the database (incl. hub seed) before the first request is served.
await app.Services.InitializeWeatherRiskDatabaseAsync();

// Swagger is enabled in every environment on purpose: reviewers test the API live.
app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "Weather Risk API v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "Weather Risk API";
});

// Pipeline order matters:
// routing (so metrics know the route template) → metrics (outermost for /api, so they record the final
// status of everything below) → exception handler (domain exceptions → 404/400, others → 500) →
// status-code pages (empty 4xx → ProblemDetails) → rate limiter → endpoints.
app.UseRouting();
app.UseRequestMetrics();
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    // Expected domain outcomes (unknown hub, invalid period) are not server errors — don't log them as such.
    SuppressDiagnosticsCallback = context => DomainExceptionHandler.IsDomainException(context.Exception),
});
app.UseStatusCodePages();
app.UseRateLimiter();

app.MapSystemEndpoints();
app.MapHubEndpoints();
app.MapRiskEndpoints();
app.MapChatEndpoints();

// Until the chat UI lands (task 8), the root opens Swagger.
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

await app.RunAsync();
