using Azure.Data.Tables;

namespace FileService.Infrastructure.Storage;

public class UploadSessionRepository : IUploadSessionRepository
{
    private readonly TableClient? _table;
    private readonly List<UploadSession>? _inMemory;

    public UploadSessionRepository(FileService.Infrastructure.Storage.BlobStorageOptions opts)
    {
        var conn = opts.ConnectionString;
        var tableName = "uploadSessions";
        if (string.IsNullOrWhiteSpace(conn))
        {
            // Fallback to an in-memory store when no connection string is provided
            // (useful for development and tests where table storage isn't configured).
            _inMemory = new List<UploadSession>();
            _table = null;
            return;
        }

        var service = new TableServiceClient(conn);
        _table = service.GetTableClient(tableName);
        _table.CreateIfNotExists();
        _inMemory = null;
    }

    public async Task CreateAsync(string blobPath, string fileName, string contentType, long totalBytes, CancellationToken ct = default)
    {
        var entity = new UploadSession(blobPath)
        {
            FileName = fileName,
            ContentType = contentType,
            TotalBytes = totalBytes,
            UploadedBytes = 0,
            Committed = false,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        };
        if (_inMemory != null)
        {
            lock (_inMemory)
            {
                _inMemory.RemoveAll(e => e.RowKey == blobPath);
                _inMemory.Add(entity);
            }
            return;
        }
    await _table!.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken: ct);
    }

    public async Task<UploadSession?> GetAsync(string blobPath, CancellationToken ct = default)
    {
        if (_inMemory != null)
        {
            lock (_inMemory)
            {
                return _inMemory.FirstOrDefault(i => i.RowKey == blobPath);
            }
        }
        try
        {
            var resp = await _table!.GetEntityAsync<UploadSession>("UploadSession", blobPath, cancellationToken: ct);
            return resp.Value;
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task AddUploadedBytesAsync(string blobPath, long addedBytes, CancellationToken ct = default)
    {
        if (_inMemory != null)
        {
            lock (_inMemory)
            {
                var e = _inMemory.FirstOrDefault(i => i.RowKey == blobPath);
                if (e != null) e.UploadedBytes += addedBytes;
            }
            return;
        }
        var entity = await GetAsync(blobPath, ct);
        if (entity == null) return;
        entity.UploadedBytes += addedBytes;
    await _table!.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken: ct);
    }

    public async Task MarkCommittedAsync(string blobPath, CancellationToken ct = default)
    {
        if (_inMemory != null)
        {
            lock (_inMemory)
            {
                var e = _inMemory.FirstOrDefault(i => i.RowKey == blobPath);
                if (e != null) e.Committed = true;
            }
            return;
        }
        var entity = await GetAsync(blobPath, ct);
        if (entity == null) return;
        entity.Committed = true;
    await _table!.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken: ct);
    }

    public async Task DeleteAsync(string blobPath, CancellationToken ct = default)
    {
        if (_inMemory != null)
        {
            lock (_inMemory)
            {
                _inMemory.RemoveAll(i => i.RowKey == blobPath);
            }
            return;
        }
    await _table!.DeleteEntityAsync("UploadSession", blobPath, cancellationToken: ct);
    }

    public async IAsyncEnumerable<UploadSession> QueryExpiredAsync(DateTimeOffset before, int maxResults = 500, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // Filter by PartitionKey and ExpiresAt
        if (_inMemory != null)
        {
            List<UploadSession> snapshot;
            lock (_inMemory)
            {
                snapshot = _inMemory.Where(i => i.ExpiresAt < before).Take(maxResults).ToList();
            }
            foreach (var entity in snapshot)
            {
                yield return entity;
                await Task.Yield();
            }
            yield break;
        }

        var filter = $"PartitionKey eq 'UploadSession' and ExpiresAt lt datetime'{before.UtcDateTime.ToString("o")}'";
        var count = 0;
    await foreach (var entity in _table!.QueryAsync<UploadSession>(filter, cancellationToken: ct))
        {
            yield return entity;
            count++;
            if (count >= maxResults) yield break;
        }
    }
}
