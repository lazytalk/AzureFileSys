using Azure;
using Azure.Data.Tables;
using FileService.Core.Entities;
using FileService.Core.Interfaces;

namespace FileService.Infrastructure.Data;

public class TableStorageFileMetadataRepository : IFileMetadataRepository
{
    private readonly TableClient _tableClient;

    public TableStorageFileMetadataRepository(TableServiceClient tableServiceClient, string tableName = "FileMetadata")
    {
        _tableClient = tableServiceClient.GetTableClient(tableName);
        _tableClient.CreateIfNotExists();
    }

    public async Task<FileRecord?> GetAsync(Guid id, CancellationToken ct = default)
    {
        // We need to query across all partitions since we don't know the OwnerUserId
        // For better performance, consider storing PartitionKey in the query if available
        var query = _tableClient.QueryAsync<FileRecordEntity>(
            filter: $"RowKey eq '{id}'",
            cancellationToken: ct);

        await foreach (var entity in query)
        {
            return entity.ToFileRecord();
        }

        return null;
    }

    public async Task<IReadOnlyList<FileRecord>> ListByOwnerAsync(string ownerUserId, CancellationToken ct = default)
    {
        var results = new List<FileRecord>();
        
        // Efficient query by PartitionKey, filtered by IsUploaded stauts
        var query = _tableClient.QueryAsync<FileRecordEntity>(
            filter: $"PartitionKey eq '{ownerUserId}' and IsUploaded eq true",
            cancellationToken: ct);

        await foreach (var entity in query)
        {
            results.Add(entity.ToFileRecord());
        }

        return results;
    }

    public async Task<IReadOnlyList<FileRecord>> ListAllAsync(int take = 200, int skip = 0, CancellationToken ct = default)
    {
        var results = new List<FileRecord>();
        int count = 0;
        int skipped = 0;

        var query = _tableClient.QueryAsync<FileRecordEntity>(
            filter: "IsUploaded eq true",
            cancellationToken: ct);

        await foreach (var entity in query)
        {
            if (skipped < skip)
            {
                skipped++;
                continue;
            }

            if (count >= take)
                break;

            results.Add(entity.ToFileRecord());
            count++;
        }

        return results;
    }

    public async Task AddAsync(FileRecord record, CancellationToken ct = default)
    {
        var entity = FileRecordEntity.FromFileRecord(record);
        await _tableClient.AddEntityAsync(entity, ct);
    }

    public async Task UpdateAsync(FileRecord record, CancellationToken ct = default)
    {
        var entity = FileRecordEntity.FromFileRecord(record);
        // Uses Upsert (Merge) to update properties
        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Merge, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        // First, find the entity to get its PartitionKey
        var existing = await GetAsync(id, ct);
        if (existing != null)
        {
            await _tableClient.DeleteEntityAsync(existing.OwnerUserId, id.ToString(), cancellationToken: ct);
        }
    }
}

// Table entity class for Azure Table Storage
public class FileRecordEntity : ITableEntity
{
    // PartitionKey = OwnerUserId for efficient user-based queries
    public string PartitionKey { get; set; } = string.Empty;
    
    // RowKey = FileId (GUID as string)
    public string RowKey { get; set; } = string.Empty;
    
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    // File properties
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public string BlobPath { get; set; } = string.Empty;
    public bool IsUploaded { get; set; }

    public static FileRecordEntity FromFileRecord(FileRecord record)
    {
        return new FileRecordEntity
        {
            PartitionKey = record.OwnerUserId,
            RowKey = record.Id.ToString(),
            FileName = record.FileName,
            ContentType = record.ContentType,
            SizeBytes = record.SizeBytes,
            UploadedAt = record.UploadedAt,
            BlobPath = record.BlobPath,
            IsUploaded = record.IsUploaded
        };
    }

    public FileRecord ToFileRecord()
    {
        return new FileRecord
        {
            Id = Guid.Parse(RowKey),
            OwnerUserId = PartitionKey,
            FileName = FileName,
            ContentType = ContentType,
            SizeBytes = SizeBytes,
            UploadedAt = UploadedAt,
            BlobPath = BlobPath,
            IsUploaded = IsUploaded
        };
    }
}
