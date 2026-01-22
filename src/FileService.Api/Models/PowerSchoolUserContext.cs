namespace FileService.Api.Models;

/// <summary>
/// Represents the authenticated PowerSchool user context extracted from request headers.
/// </summary>
public class PowerSchoolUserContext
{
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public bool IsAdmin => Role.Equals("admin", StringComparison.OrdinalIgnoreCase);
}
