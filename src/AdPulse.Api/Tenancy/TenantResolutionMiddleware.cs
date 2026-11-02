using AdPulse.Domain.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace AdPulse.Api.Tenancy;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext httpContext)
    {
        if (RequiresTenantMembership(httpContext))
        {
            try
            {
                var resolver = httpContext.RequestServices.GetRequiredService<TenantContextResolver>();
                var requestContext = httpContext.RequestServices.GetRequiredService<TenantRequestContext>();
                var resolved = await resolver.ResolveAsync(httpContext.User, httpContext.RequestAborted);
                requestContext.SetResolved(resolved);
            }
            catch (TenantAccessDeniedException)
            {
                httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
        }

        await next(httpContext);
    }

    private static bool RequiresTenantMembership(HttpContext httpContext)
    {
        var endpoint = httpContext.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            return false;
        }

        return httpContext.User.Identity?.IsAuthenticated == true;
    }
}
