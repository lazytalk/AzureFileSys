using System.Security.Cryptography;
using System.Text;

namespace FileService.Api.Endpoints;

/// <summary>
/// PowerSchool Authentication API endpoints for development and testing.
/// These endpoints are only available in Development mode.
/// </summary>
public static class PowerSchoolAuthEndpoints
{
    public static void MapPowerSchoolAuthEndpoints(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return;

        app.MapPost("/dev/powerschool/token", GenerateTokenHandler);
        app.MapPost("/dev/powerschool/validate", ValidateTokenHandler);
    }

    private static IResult GenerateTokenHandler(
        string userId,
        string role,
        string? secret)
    {
        try
        {
            // Very simple token mimic: base64(userId|role|ticks|hmac)
            var ticks = DateTimeOffset.UtcNow.Ticks;
            var key = secret ?? "dev-shared-secret";
            var raw = $"{userId}|{role}|{ticks}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var sig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(raw)));
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw + "|" + sig));
            return Results.Ok(new { token });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEV-TOKEN] Error generating token: {ex.Message}");
            return Results.Problem($"Failed to generate token: {ex.Message}");
        }
    }

    private static IResult ValidateTokenHandler(string token, string? secret)
    {
        try
        {
            var key = secret ?? "dev-shared-secret";
            var data = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parts = data.Split('|');
            if (parts.Length != 4) return Results.BadRequest("Malformed token");
            
            var user = parts[0];
            var role = parts[1];
            var ticks = parts[2];
            var sig = parts[3];
            var raw = $"{user}|{role}|{ticks}";
            
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(raw)));
            if (!expected.Equals(sig, StringComparison.OrdinalIgnoreCase)) 
                return Results.Unauthorized();
            
            return Results.Ok(new { user, role });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEV-TOKEN] Error validating token: {ex.Message}");
            return Results.BadRequest("Invalid token format");
        }
    }
}
