namespace FileService.Api.Models;

/// <summary>
/// Request DTO for creating a zip export of multiple files.
/// </summary>
public class ZipExportRequestDto
{
    public string[] FilePaths { get; set; } = Array.Empty<string>();
    public string ZipFileName { get; set; } = "export.zip";
}
