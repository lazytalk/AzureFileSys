using System.Security.Cryptography;
using System.Text;

namespace FileService.Api.Middleware;

/// <summary>
/// Validates HMAC-SHA256 signatures on incoming requests to prevent unauthorized access.
/// Requires X-Signature and X-Timestamp headers. Rejects requests older than 5 minutes.
/// </summary>
public class HmacAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string? _hmacSecret;
    private readonly ILogger<HmacAuthenticationMiddleware> _logger;
    private readonly bool _isEnabled;

    public HmacAuthenticationMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<HmacAuthenticationMiddleware> logger)
    {
        _next = next;
        _hmacSecret = configuration.GetValue<string>("Security:HmacSharedSecret");
        _logger = logger;
        _isEnabled = !string.IsNullOrWhiteSpace(_hmacSecret);

        if (!_isEnabled)
        {
            _logger.LogWarning("HMAC authentication is DISABLED. Configure Security:HmacSharedSecret to enable.");
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip HMAC validation if not enabled
        if (!_isEnabled)
        {
            await _next(context);
            return;
        }

        // Skip validation for specific paths
        if (ShouldSkipValidation(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Validate signature
        var validationResult = ValidateSignature(context);
        if (!validationResult.IsValid)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = validationResult.ErrorMessage });
            return;
        }

        await _next(context);
    }

    private static bool ShouldSkipValidation(PathString path)
    {
        return path.StartsWithSegments("/swagger") ||
               path.StartsWithSegments("/_framework") ||
               path.StartsWithSegments("/_vs") ||
               path.StartsWithSegments("/api/health") ||
               path.StartsWithSegments("/dev/powerschool") ||
               path.Value?.EndsWith(".html") == true ||
               path.Value?.EndsWith(".css") == true ||
               path.Value?.EndsWith(".js") == true;
    }

    private ValidationResult ValidateSignature(HttpContext context)
    {
        // Check for required headers
        if (!context.Request.Headers.TryGetValue("X-Signature", out var signature) || string.IsNullOrWhiteSpace(signature))
        {
            return ValidationResult.Failure("Missing X-Signature header");
        }

        if (!context.Request.Headers.TryGetValue("X-Timestamp", out var timestampStr) || string.IsNullOrWhiteSpace(timestampStr))
        {
            return ValidationResult.Failure("Missing X-Timestamp header");
        }

        // Parse and validate timestamp
        if (!long.TryParse(timestampStr, out var timestamp))
        {
            return ValidationResult.Failure("Invalid X-Timestamp format");
        }

        var requestTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        var now = DateTimeOffset.UtcNow;
        var ageMinutes = Math.Abs((now - requestTime).TotalMinutes);

        if (ageMinutes > 5)
        {
            _logger.LogWarning("Request timestamp expired. Age: {Age} minutes", ageMinutes);
            return ValidationResult.Failure("Request timestamp expired or invalid");
        }

        // Extract request details
        var psUser = context.Request.Headers["X-PowerSchool-User"].ToString();
        var psRole = context.Request.Headers["X-PowerSchool-Role"].ToString();
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? "";

        // Compute expected signature
        var message = $"{timestamp}{method}{path}{psUser}{psRole}";
        var expectedSignature = ComputeHmacSignature(message, _hmacSecret!);

        if (!string.Equals(signature, expectedSignature, StringComparison.Ordinal))
        {
            _logger.LogWarning("Invalid HMAC signature for user {User}, path {Path}", psUser, path);
            return ValidationResult.Failure("Invalid signature");
        }

        return ValidationResult.Success();
    }

    private static string ComputeHmacSignature(string message, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return Convert.ToBase64String(hash);
    }

    private record ValidationResult(bool IsValid, string? ErrorMessage = null)
    {
        public static ValidationResult Success() => new(true);
        public static ValidationResult Failure(string error) => new(false, error);
    }
}
