using AdPulse.Api.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);
var identity = builder.Configuration.GetSection("Identity").Get<IdentityOptions>() ?? new IdentityOptions();
identity.ValidateProviderConfiguration();

builder.Services.AddSingleton(identity);
builder.Services.AddSingleton<IdentityTokenValidator>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.Authority = identity.Authority;
        options.Audience = identity.Audience;
        options.RequireHttpsMetadata = identity.RequireHttpsMetadata;
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidateLifetime = true;
        options.TokenValidationParameters.ClockSkew = TimeSpan.Zero;
    });
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.Run();

public partial class Program;
