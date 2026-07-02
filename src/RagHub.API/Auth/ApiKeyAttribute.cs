using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using RagHub.Core.Settings;

namespace RagHub.API.Auth;

// Marks an endpoint as part of the external "RagHub" surface: callers (other teams,
// edge clients) must send a valid key via the X-Api-Key header. Demo-grade only —
// keys live in config, not a DB; rotate/revoke means editing appsettings.
public class ApiKeyAttribute : Attribute, IAsyncActionFilter
{
    private const string HeaderName = "X-Api-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var settings = context.HttpContext.RequestServices.GetRequiredService<IOptions<RagSettings>>().Value;

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var provided) ||
            string.IsNullOrWhiteSpace(provided))
        {
            context.Result = new Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult(
                new { error = $"Missing '{HeaderName}' header." });
            return;
        }

        var match = settings.ApiKeys.FirstOrDefault(k => k.Key == provided.ToString());
        if (match is null)
        {
            context.Result = new Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult(
                new { error = "Invalid API key." });
            return;
        }

        context.HttpContext.Items["ApiKeyOwner"] = match.Owner;
        await next();
    }
}
