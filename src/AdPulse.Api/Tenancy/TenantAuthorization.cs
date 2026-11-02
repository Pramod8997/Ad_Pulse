using Microsoft.AspNetCore.Authorization;

namespace AdPulse.Api.Tenancy;

public static class TenantAuthorizationPolicies
{
    public const string Member = "TenantMember";
}

public sealed class TenantMemberRequirement : IAuthorizationRequirement;

public sealed class TenantMemberAuthorizationHandler(TenantRequestContext requestContext)
    : AuthorizationHandler<TenantMemberRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantMemberRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true && requestContext.Current is not null)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
