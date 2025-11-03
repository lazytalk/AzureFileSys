using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FileService.Api.Services;
using FileService.Core.Interfaces;
using FileService.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FileService.Tests;

public class UploadSessionCleanupTests
{
    class FakeRepo : IUploadSessionRepository
    {
        private readonly List<UploadSession> _items = new();
        public FakeRepo(IEnumerable<UploadSession>? initial = null)
        {
            if (initial != null) _items.AddRange(initial);
        }
        public Task CreateAsync(string blobPath, string fileName, string contentType, long totalBytes, CancellationToken ct = default)
        {
            var e = new UploadSession(blobPath) { FileName = fileName, ContentType = contentType, TotalBytes = totalBytes, UploadedBytes = 0, Committed = false, ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1) };
            _items.Add(e);
            return Task.CompletedTask;
        }

        public Task<UploadSession?> GetAsync(string blobPath, CancellationToken ct = default)
        {
            return Task.FromResult(_items.FirstOrDefault(i => i.RowKey == blobPath));
        }

        public Task AddUploadedBytesAsync(string blobPath, long addedBytes, CancellationToken ct = default)
        {
            var e = _items.FirstOrDefault(i => i.RowKey == blobPath);
            if (e != null) e.UploadedBytes += addedBytes;
            return Task.CompletedTask;
        }

        public Task MarkCommittedAsync(string blobPath, CancellationToken ct = default)
        {
            var e = _items.FirstOrDefault(i => i.RowKey == blobPath);
            if (e != null) e.Committed = true;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string blobPath, CancellationToken ct = default)
        {
            _items.RemoveAll(i => i.RowKey == blobPath);
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<UploadSession> QueryExpiredAsync(DateTimeOffset before, int maxResults = 500, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            // Snapshot the list to avoid collection-modified exceptions when the caller
            // may delete items while iterating (the production implementation queries
            // Table storage which yields a stable enumeration).
            var snapshot = _items.Where(i => i.ExpiresAt < before).Take(maxResults).ToList();
            foreach (var e in snapshot)
            {
                yield return e;
                await Task.Yield();
            }
        }
    }

    class FakeStorage : IFileStorageService
    {
        public List<string> Aborted = new();
        private readonly int _failAttempts;
        private int _attempts = 0;
        public FakeStorage(int failAttempts = 0) { _failAttempts = failAttempts; }
        public Task<string> UploadAsync(string blobPath, System.IO.Stream content, string contentType, CancellationToken ct = default) => Task.FromResult(blobPath);
        public Task UploadBlockAsync(string blobPath, string base64BlockId, System.IO.Stream content, CancellationToken ct = default) => Task.CompletedTask;
        public Task CommitBlocksAsync(string blobPath, IEnumerable<string> base64BlockIds, string contentType, CancellationToken ct = default) => Task.CompletedTask;
        public Task AbortUploadAsync(string blobPath, CancellationToken ct = default)
        {
            _attempts++;
            if (_attempts <= _failAttempts)
                throw new System.Exception("transient");
            Aborted.Add(blobPath);
            return Task.CompletedTask;
        }
        public Task<System.IO.Stream?> DownloadAsync(string blobPath, CancellationToken ct = default) => Task.FromResult<System.IO.Stream?>(null);
        public Task DeleteAsync(string blobPath, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string> GetReadSasUrlAsync(string blobPath, TimeSpan ttl, CancellationToken ct = default) => Task.FromResult(string.Empty);
    }

    [Fact]
    public async Task CleanupService_DeletesExpiredSessions_AfterRetries()
    {
        // Arrange
        var session = new UploadSession("blob1") { ExpiresAt = DateTimeOffset.UtcNow.AddHours(-2) };
        var repo = new FakeRepo(new[] { session });
        var storage = new FakeStorage(failAttempts: 2); // two transient fails then success
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            { "Upload:Cleanup:IntervalMinutes", "1" },
            { "Upload:Cleanup:MaxSessionsPerRun", "10" },
            { "Upload:Cleanup:RetryCount", "3" },
            { "Upload:Cleanup:BaseDelayMs", "10" },
            { "Upload:Cleanup:MaxDelayMs", "100" },
            { "Upload:Cleanup:EnableBlockListCleanup", "false" }
        }).Build();
        var logger = new NullLogger<UploadSessionCleanupService>();
        var svc = new UploadSessionCleanupService(repo, storage, config, logger);

        // Act
        var cts = new CancellationTokenSource();
        await svc.CleanupOnceAsync(cts.Token);

        // Assert
        Assert.Contains("blob1", storage.Aborted);
        var remaining = await repo.GetAsync("blob1");
        Assert.Null(remaining);
    }
}
