using WeatherRisk.Api.Endpoints;
using WeatherRisk.Api.Observability;
using WeatherRisk.Api.RateLimiting;

// Composition root: service registration and HTTP pipeline. Feature wiring lives in extension methods.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddObservability(builder.Configuration);
builder.Services.AddApiRateLimiting(builder.Configuration);

var app = builder.Build();

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

app.Run();
