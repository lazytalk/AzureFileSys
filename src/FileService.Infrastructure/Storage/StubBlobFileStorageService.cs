using FileService.Core.Interfaces;

namespace FileService.Infrastructure.Storage;

// Placeholder that mimics blob storage until Azure Blob is configured.
public class StubBlobFileStorageService : IFileStorageService
{
    private readonly Dictionary<string, (byte[] Content, string ContentType)> _blobs = new();

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
}
