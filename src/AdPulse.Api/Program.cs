using AdPulse.Api.Auth;
using AdPulse.Api.Tenancy;
using AdPulse.Domain.Tenancy;
using AdPulse.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var identity = builder.Configuration.GetSection("Identity").Get<IdentityOptions>() ?? new IdentityOptions();
var useTestIdentity = builder.Environment.IsEnvironment("Testing");

if (!useTestIdentity)
{
    identity.ValidateProviderConfiguration();
}

var connectionString = builder.Configuration.GetConnectionString("AdPulse");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:AdPulse is required.");
}

builder.Services.AddSingleton(identity);
builder.Services.AddSingleton<IdentityTokenValidator>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.Audience = identity.Audience;
        if (useTestIdentity)
        {
            // Test tokens use an explicit local key and never cause discovery against a provider.
            options.TokenValidationParameters = identity.CreateTestValidationParameters();
        }
        else
        {
            options.Authority = identity.Authority;
            options.RequireHttpsMetadata = identity.RequireHttpsMetadata;
            options.TokenValidationParameters.ValidateIssuer = true;
            options.TokenValidationParameters.ValidateAudience = true;
            options.TokenValidationParameters.ValidateLifetime = true;
            options.TokenValidationParameters.ClockSkew = TimeSpan.Zero;
        }
    });
builder.Services.AddAuthorization(options =>
{
    static void RequireTenantMember(AuthorizationPolicyBuilder policy)
    {
        policy.RequireAuthenticatedUser();
        policy.AddRequirements(new TenantMemberRequirement());
    }

    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .AddRequirements(new TenantMemberRequirement())
        .Build();
    options.AddPolicy(TenantAuthorizationPolicies.Member, RequireTenantMember);
});
builder.Services.AddScoped<IAuthorizationHandler, TenantMemberAuthorizationHandler>();
builder.Services.AddControllers();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TenantRequestContext>();
builder.Services.AddScoped<TenantContextResolver>();
builder.Services.AddScoped<TenantScope>(services => services.GetRequiredService<TenantRequestContext>().CandidateScope);
builder.Services.AddDbContext<AdPulseDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddScoped<ITenantMembershipStore, EfTenantMembershipStore>();
builder.Services.AddScoped<TenantContext>(services => services.GetRequiredService<TenantRequestContext>().RequireResolved());

var app = builder.Build();
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapControllers();
app.Run();

public partial class Program;
