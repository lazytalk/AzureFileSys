using System.Runtime.CompilerServices;

namespace FileService.Infrastructure.Storage;

public interface IUploadSessionRepository
{
    Task CreateAsync(string blobPath, string fileName, string contentType, long totalBytes, CancellationToken ct = default);
    Task<UploadSession?> GetAsync(string blobPath, CancellationToken ct = default);
    Task AddUploadedBytesAsync(string blobPath, long addedBytes, CancellationToken ct = default);
    Task MarkCommittedAsync(string blobPath, CancellationToken ct = default);
    Task DeleteAsync(string blobPath, CancellationToken ct = default);

    /// <summary>
    /// Query upload sessions that have ExpiresAt earlier than the provided cutoff.
    /// Returns at most <paramref name="maxResults"/> items.
    /// </summary>
    IAsyncEnumerable<UploadSession> QueryExpiredAsync(DateTimeOffset before, int maxResults = 500, CancellationToken ct = default);
}
