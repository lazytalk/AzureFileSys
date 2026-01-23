using FileService.Api.Models;
using FileService.Api.Services;

namespace FileService.Api.Middleware;

/// <summary>
/// Middleware for PowerSchool authentication with OpenID session validation
/// </summary>
public class PowerSchoolAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly bool _isDevelopment;
    private readonly HashSet<string> _exemptPaths;

    public PowerSchoolAuthenticationMiddleware(RequestDelegate next, IWebHostEnvironment env)
    {
        _next = next;
        _isDevelopment = env.IsDevelopment();
        _exemptPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/authenticate",
            "/verify",
            "/api/auth/session-info",
            "/api/auth/logout"
        };
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip authentication for public OpenID endpoints
        if (IsExemptPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var userContext = context.RequestServices.GetRequiredService<PowerSchoolUserContext>();

        // Dev shortcut: allow ?devUser=xxx in development mode
        if (_isDevelopment && context.Request.Query.TryGetValue("devUser", out var devUser))
        {
            userContext.UserId = devUser!;
            userContext.Role = context.Request.Query.TryGetValue("role", out var r) ? r.ToString() : "user";
            await _next(context);
            return;
        }

        // Extract PowerSchool identity headers (prefer numeric UserId/DCID for validation)
        var (hasIdentity, canonicalUserId) = ExtractUserIdentity(context);

        if (!hasIdentity)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new 
            { 
                error = "Missing PowerSchool identity header 'X-PowerSchool-UserId'/'X-Powerschool-Userid' or 'X-PowerSchool-User'" 
            });
            return;
        }

        // Validate session cookie matches the numeric user id (defense-in-depth)
        if (!OpenIdRelyingPartyExtensions.ValidateSessionAndUser(context, canonicalUserId))
        {
            await WriteAuthenticationFailureResponse(context);
            return;
        }

        // Extract role and populate user context
        var role = context.Request.Headers.TryGetValue("X-PowerSchool-Role", out var roleHeader) 
            ? roleHeader.ToString() 
            : "user";
        
        userContext.UserId = canonicalUserId;
        userContext.Role = role;

        await _next(context);
    }

    private bool IsExemptPath(PathString path)
    {
        return _exemptPaths.Any(exemptPath => path.StartsWithSegments(exemptPath));
    }

    private static (bool hasIdentity, string canonicalUserId) ExtractUserIdentity(HttpContext context)
    {
        var hasUser = context.Request.Headers.TryGetValue("X-PowerSchool-User", out var userHeader) 
            && !string.IsNullOrWhiteSpace(userHeader);
        
        var hasUserId = context.Request.Headers.TryGetValue("X-PowerSchool-UserId", out var userIdHeader) 
            && !string.IsNullOrWhiteSpace(userIdHeader);
        
        // Some environments may send different casing like X-Powerschool-Userid
        if (!hasUserId && context.Request.Headers.TryGetValue("X-Powerschool-Userid", out var userIdHeaderAlt) 
            && !string.IsNullOrWhiteSpace(userIdHeaderAlt))
        {
            userIdHeader = userIdHeaderAlt;
            hasUserId = true;
        }

        if (!hasUserId && !hasUser)
        {
            return (false, string.Empty);
        }

        var canonicalUserId = hasUserId ? userIdHeader.ToString() : userHeader.ToString();
        return (true, canonicalUserId);
    }

    private static async Task WriteAuthenticationFailureResponse(HttpContext context)
    {
        context.Response.StatusCode = 401;
        var config = context.RequestServices.GetRequiredService<IConfiguration>();
        var enableDetailed = config.GetSection("Features").GetValue<bool>("EnableDetailedErrors");
        
        var debug = enableDetailed ? new
        {
            reason = context.Items["AuthDebugReason"],
            details = context.Items["AuthDebugDetails"]
        } : null;

        await context.Response.WriteAsJsonAsync(new
        {
            error = "Authentication failed",
            details = "Session validation failed. Please authenticate through PowerSchool.",
            debug
        });
    }
}

/// <summary>
/// Extension methods for registering PowerSchool authentication middleware
/// </summary>
public static class PowerSchoolAuthenticationMiddlewareExtensions
{
    /// <summary>
    /// Adds PowerSchool authentication middleware to the application pipeline
    /// </summary>
    public static IApplicationBuilder UsePowerSchoolAuthentication(this IApplicationBuilder app)
    {
        return app.UseMiddleware<PowerSchoolAuthenticationMiddleware>();
    }
}
