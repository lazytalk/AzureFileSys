using FileService.Core.Interfaces;

namespace FileService.Infrastructure.Storage;

// Placeholder that mimics blob storage until Azure Blob is configured.
public class StubBlobFileStorageService : IFileStorageService
{
    private readonly Dictionary<string, (byte[] Content, string ContentType)> _blobs = new();
    // Staged blocks storage for resumable uploads: (blobPath -> (blockId -> bytes))
    private readonly Dictionary<string, Dictionary<string, byte[]>> _stagedBlocks = new();
    private readonly object _blocksLock = new();

    public Task DeleteAsync(string blobPath, CancellationToken ct = default)
    {
        _blobs.Remove(blobPath);
        return Task.CompletedTask;
    }

    public Task<Stream?> DownloadAsync(string blobPath, CancellationToken ct = default)
    {
        if (_blobs.TryGetValue(blobPath, out var value))
        {
            return Task.FromResult<Stream?>(new MemoryStream(value.Content));
        }
        return Task.FromResult<Stream?>(null);
    }

    public Task<string> GetReadSasUrlAsync(string blobPath, TimeSpan ttl, CancellationToken ct = default)
    {
        // In real implementation, generate SAS. Here we just return a pseudo URL.
        return Task.FromResult($"stub://{blobPath}?ttl={(int)ttl.TotalSeconds}");
    }

    public async Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        _blobs[blobPath] = (ms.ToArray(), contentType);
        return blobPath;
    }

    public Task UploadBlockAsync(string blobPath, string base64BlockId, Stream content, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        var data = ms.ToArray();
        lock (_blocksLock)
        {
            if (!_stagedBlocks.TryGetValue(blobPath, out var map))
            {
                map = new Dictionary<string, byte[]>();
                _stagedBlocks[blobPath] = map;
            }
            map[base64BlockId] = data;
        }
        return Task.CompletedTask;
    }

    public Task CommitBlocksAsync(string blobPath, IEnumerable<string> base64BlockIds, string contentType, CancellationToken ct = default)
    {
        List<byte> combined = new();
        lock (_blocksLock)
        {
            if (_stagedBlocks.TryGetValue(blobPath, out var map))
            {
                foreach (var id in base64BlockIds)
                {
                    if (map.TryGetValue(id, out var part))
                    {
                        combined.AddRange(part);
                    }
                    else
                    {
                        // Missing block - treat as error by skipping
                    }
                }
                // Remove staged blocks after commit
                _stagedBlocks.Remove(blobPath);
            }
        }
        _blobs[blobPath] = (combined.ToArray(), contentType);
        return Task.CompletedTask;
    }

    public Task AbortUploadAsync(string blobPath, CancellationToken ct = default)
    {
        lock (_blocksLock)
        {
            _stagedBlocks.Remove(blobPath);
        }
        return Task.CompletedTask;
    }
}
