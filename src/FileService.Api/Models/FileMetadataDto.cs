namespace FileService.Api.Models;

/// <summary>
/// Data transfer object for file metadata responses.
/// </summary>
public class FileMetadataDto
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
}
