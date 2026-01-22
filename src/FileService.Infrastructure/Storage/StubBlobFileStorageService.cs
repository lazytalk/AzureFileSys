using FileService.Core.Interfaces;

namespace FileService.Infrastructure.Storage;

// Placeholder that mimics blob storage until Azure Blob is configured.
public class StubBlobFileStorageService : IFileStorageService
{
    private readonly Dictionary<string, (byte[] Content, string ContentType)> _blobs = new();
    private readonly Dictionary<string, DateTimeOffset> _lastModified = new();

    public Task DeleteAsync(string blobPath, CancellationToken ct = default)
    {
        _blobs.Remove(blobPath);
        _lastModified.Remove(blobPath);
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

    public Task<string> GetWriteSasUrlAsync(string blobPath, TimeSpan ttl, CancellationToken ct = default)
    {
        // Return a local URL that our dev/test tools can "PUT" to if they want to simulate upload.
        // In a real local scenario with Azurite, this would be http://127.0.0.1:10000/...
        // For this in-memory stub, we'll return a special stub-scheme or a local relative path that the client might handle specially,
        // OR we just return a dummy that will fail if actually used, relying on tests to mock the upload part?
        // Let's assume for now we use a stub scheme.
        return Task.FromResult($"stub://{blobPath}?permission=write&ttl={(int)ttl.TotalSeconds}");
    }

    public Task<long?> GetBlobSizeAsync(string blobPath, CancellationToken ct = default)
    {
        if (_blobs.TryGetValue(blobPath, out var value))
        {
            return Task.FromResult<long?>((long)value.Content.Length);
        }
        return Task.FromResult<long?>(null);
    }

    public async Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        _blobs[blobPath] = (ms.ToArray(), contentType);
        _lastModified[blobPath] = DateTimeOffset.UtcNow;
        return blobPath;
    }

    public Task<Stream> OpenWriteAsync(string blobPath, string contentType, CancellationToken ct = default)
    {
        // For the stub, we return a MemoryStream wrapper that saves to the dictionary on disposal
        return Task.FromResult<Stream>(new StubBlobStream(blobPath, contentType, _blobs, _lastModified));
    }

    private class StubBlobStream : MemoryStream
    {
        private readonly string _path;
        private readonly string _contentType;
        private readonly Dictionary<string, (byte[], string)> _store;
        private readonly Dictionary<string, DateTimeOffset> _meta;
        public StubBlobStream(string path, string contentType, Dictionary<string, (byte[], string)> store, Dictionary<string, DateTimeOffset> meta)
        {
            _path = path;
            _contentType = contentType;
            _store = store;
            _meta = meta;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _store[_path] = (this.ToArray(), _contentType);
                _meta[_path] = DateTimeOffset.UtcNow;
            }
            base.Dispose(disposing);
        }
    }

    public Task<IReadOnlyList<BlobItemInfo>> ListAsync(string prefix, CancellationToken ct = default)
    {
        var list = _blobs.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(k => new BlobItemInfo(k, _lastModified.TryGetValue(k, out var ts) ? ts : (DateTimeOffset?)null))
            .ToList();
        return Task.FromResult<IReadOnlyList<BlobItemInfo>>(list);
    }
}
