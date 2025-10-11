namespace FileService.Core.Models;

public record FileListItemDto(Guid Id, string FileName, long SizeBytes, string ContentType, DateTimeOffset UploadedAt, string OwnerUserId);
