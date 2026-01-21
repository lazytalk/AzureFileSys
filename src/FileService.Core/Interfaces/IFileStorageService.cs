namespace FileService.Core.Interfaces;

public interface IFileStorageService
{
    Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken ct = default);
    Task<Stream?> DownloadAsync(string blobPath, CancellationToken ct = default);
    Task DeleteAsync(string blobPath, CancellationToken ct = default);
    Task<string> GetReadSasUrlAsync(string blobPath, TimeSpan ttl, CancellationToken ct = default);
    Task<string> GetWriteSasUrlAsync(string blobPath, TimeSpan ttl, CancellationToken ct = default);
    Task<long?> GetBlobSizeAsync(string blobPath, CancellationToken ct = default);
    Task<Stream> OpenWriteAsync(string blobPath, string contentType, CancellationToken ct = default);
}
