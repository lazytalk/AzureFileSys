namespace FileService.Api.Models;

/// <summary>
/// Request DTO for initiating a file upload.
/// </summary>
public class BeginUploadRequest
{
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long SizeBytes { get; set; }
}
