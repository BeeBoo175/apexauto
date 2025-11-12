using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Dealership.Application.Accounts;
using Dealership.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Dealership.Api.Configuration;

public static class ApiAuthentication
{
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((options, settings) =>
            {
                var jwt = settings.Value;
                options.MapInboundClaims = false;
                options.IncludeErrorDetails = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
                options.Events.OnTokenValidated = ValidateSessionAsync;
                options.Events.OnChallenge = context =>
                {
                    context.Response.Headers.CacheControl = "no-store";
                    return Task.CompletedTask;
                };
            });
        services.AddAuthorization();
        return services;
    }

    private static async Task ValidateSessionAsync(TokenValidatedContext context)
    {
        var user = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var session = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        var sessions = context.HttpContext.RequestServices.GetRequiredService<IAccountSessionStore>();

        if (!Guid.TryParse(user, out var userId) || userId == Guid.Empty ||
            !Guid.TryParse(session, out var sessionId) || sessionId == Guid.Empty ||
            !await sessions.IsActiveAsync(userId, sessionId, context.HttpContext.RequestAborted))
        {
            context.Fail("Session is unavailable.");
        }
    }
}
