namespace FileService.Core.Interfaces;

public interface IFileStorageService
{
    Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken ct = default);
    // Resumable upload support using block blobs
    Task UploadBlockAsync(string blobPath, string base64BlockId, Stream content, CancellationToken ct = default);
    Task CommitBlocksAsync(string blobPath, IEnumerable<string> base64BlockIds, string contentType, CancellationToken ct = default);
    Task AbortUploadAsync(string blobPath, CancellationToken ct = default);
    Task<Stream?> DownloadAsync(string blobPath, CancellationToken ct = default);
    Task DeleteAsync(string blobPath, CancellationToken ct = default);
    Task<string> GetReadSasUrlAsync(string blobPath, TimeSpan ttl, CancellationToken ct = default);
}
