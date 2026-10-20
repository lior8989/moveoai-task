using Microsoft.Net.Http.Headers;

namespace WeatherRisk.Api.ChatUi;

/// <summary>Serves the static chat UI (<c>wwwroot</c>) at the site root.</summary>
public static class ChatUiExtensions
{
    // Same-origin only: the UI has no inline scripts/styles and calls only this API. data: is for the emoji favicon.
    private const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; " +
        "frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

    /// <summary>
    /// Serves <c>index.html</c> for <c>/</c> and the UI's static assets, with security headers and
    /// revalidation caching (assets aren't fingerprinted, so browsers must check ETags instead of using stale copies).
    /// </summary>
    /// <param name="app">Application builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IApplicationBuilder UseChatUi(this IApplicationBuilder app)
    {
        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context =>
            {
                IHeaderDictionary headers = context.Context.Response.Headers;
                headers[HeaderNames.CacheControl] = "no-cache";
                headers[HeaderNames.XContentTypeOptions] = "nosniff";
                headers[HeaderNames.ContentSecurityPolicy] = ContentSecurityPolicy;
                headers["Referrer-Policy"] = "no-referrer";
            },
        });
        return app;
    }
}
