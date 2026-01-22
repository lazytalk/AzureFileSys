namespace FileService.Api.Models;

/// <summary>
/// Represents the status of an asynchronous zip export job.
/// </summary>
public class ZipJobStatus
{
    public string Status { get; set; } = "Processing"; 
    public string? DownloadUrl { get; set; }
    public string? Error { get; set; }
    public string? Progress { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddHours(2);
    public string? BlobPath { get; set; }
}
