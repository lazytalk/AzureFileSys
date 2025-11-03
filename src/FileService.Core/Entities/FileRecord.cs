namespace FileService.Core.Entities;

public class FileRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string OwnerUserId { get; set; } = string.Empty; // Owner user identifier (if your deployment tracks per-user ownership)
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
    public string BlobPath { get; set; } = string.Empty;
    // Soft delete flags: keep metadata for audit/history
    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
}
