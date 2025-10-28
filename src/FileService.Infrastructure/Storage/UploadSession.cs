using Azure;
using Azure.Data.Tables;

namespace FileService.Infrastructure.Storage;

public class UploadSession : ITableEntity
{
    public string PartitionKey { get; set; } = "UploadSession";
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long TotalBytes { get; set; }
    public long UploadedBytes { get; set; }
    public bool Committed { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }

    // ITableEntity members
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public UploadSession() { }

    public UploadSession(string blobPath)
    {
        RowKey = blobPath; // use blobPath as row key for easy lookup
    }
}
