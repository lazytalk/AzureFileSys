using Azure.Data.Tables;
using FileService.Core.Entities;
using FileService.Core.Interfaces;

namespace FileService.Infrastructure.Storage;

public class AzureTableFileMetadataRepository : IFileMetadataRepository
{
    private readonly TableClient _tableClient;

    public AzureTableFileMetadataRepository(TableServiceClient tableServiceClient)
    {
        _tableClient = tableServiceClient.GetTableClient("filerecords");
        // Create table if it doesn't exist
        _tableClient.CreateIfNotExists();
    }

    public async Task AddAsync(FileRecord record, CancellationToken ct = default)
    {
        var entity = new FileRecordTableEntity(record);
        await _tableClient.AddEntityAsync(entity, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        // For delete, we need to find the entity first to get the PartitionKey and RowKey
        var entity = await GetTableEntityAsync(id, ct);
        if (entity != null)
        {
            await _tableClient.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, cancellationToken: ct);
        }
    }

    public async Task<FileRecord?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await GetTableEntityAsync(id, ct);
        return entity?.ToFileRecord();
    }

    public async Task<IReadOnlyList<FileRecord>> ListAllAsync(int take = 200, int skip = 0, CancellationToken ct = default)
    {
        var query = _tableClient.QueryAsync<FileRecordTableEntity>(
            select: null, 
            maxPerPage: take,
            cancellationToken: ct);

        var results = new List<FileRecord>();
        var skipped = 0;
        
        await foreach (var entity in query)
        {
            if (skipped < skip)
            {
                skipped++;
                continue;
            }
            
            results.Add(entity.ToFileRecord());
            
            if (results.Count >= take)
                break;
        }

        return results.OrderByDescending(f => f.UploadedAt).ToList();
    }

    public async Task<IReadOnlyList<FileRecord>> ListByOwnerAsync(string ownerUserId, CancellationToken ct = default)
    {
        // Query by PartitionKey (which is the OwnerUserId)
        var query = _tableClient.QueryAsync<FileRecordTableEntity>(
            filter: $"PartitionKey eq '{ownerUserId}'",
            cancellationToken: ct);

        var results = new List<FileRecord>();
        await foreach (var entity in query)
        {
            results.Add(entity.ToFileRecord());
        }

        return results.OrderByDescending(f => f.UploadedAt).ToList();
    }

    private async Task<FileRecordTableEntity?> GetTableEntityAsync(Guid id, CancellationToken ct = default)
    {
        // Since we don't know the PartitionKey, we need to query by the Id field
        var query = _tableClient.QueryAsync<FileRecordTableEntity>(
            filter: $"Id eq guid'{id}'",
            cancellationToken: ct);

        await foreach (var entity in query)
        {
            return entity; // Return first match
        }

        return null;
    }
}

// Table Storage entity - inherits from ITableEntity for Azure SDK
public class FileRecordTableEntity : ITableEntity
{
    public FileRecordTableEntity() { }

    public FileRecordTableEntity(FileRecord record)
    {
        // Use OwnerUserId as PartitionKey for efficient queries by user
        PartitionKey = record.OwnerUserId;
        // Use Id as RowKey to ensure uniqueness
        RowKey = record.Id.ToString();
        
        Id = record.Id;
        FileName = record.FileName;
        ContentType = record.ContentType;
        SizeBytes = record.SizeBytes;
        OwnerUserId = record.OwnerUserId;
        UploadedAt = record.UploadedAt;
        BlobPath = record.BlobPath;
        
        Timestamp = DateTimeOffset.UtcNow;
    }

    // ITableEntity required properties
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public Azure.ETag ETag { get; set; }

    // FileRecord properties
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string OwnerUserId { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; }
    public string BlobPath { get; set; } = string.Empty;

    public FileRecord ToFileRecord()
    {
        return new FileRecord
        {
            Id = Id,
            FileName = FileName,
            ContentType = ContentType,
            SizeBytes = SizeBytes,
            OwnerUserId = OwnerUserId,
            UploadedAt = UploadedAt,
            BlobPath = BlobPath
        };
    }
}