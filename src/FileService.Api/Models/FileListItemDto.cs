namespace FileService.Api.Models;

/// <summary>
/// Response DTO for file listing operations.
/// </summary>
public record FileListItemDto(Guid Id, string FileName, long SizeBytes, string ContentType, DateTimeOffset UploadedAt, string OwnerUserId);
