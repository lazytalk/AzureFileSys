using System.Threading;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FileService.Api.Services;

public class UploadSessionCleanupService : BackgroundService
{
    private readonly TimeSpan _interval;
    private readonly FileService.Infrastructure.Storage.IUploadSessionRepository _repo;
    private readonly FileService.Core.Interfaces.IFileStorageService _storage;
    private readonly ILogger<UploadSessionCleanupService> _logger;
    private readonly int _maxSessionsPerRun;
    private readonly int _retryCount;
    private readonly int _baseDelayMs;
    private readonly int _maxDelayMs;
    private readonly bool _enableBlockListCleanup;

    public UploadSessionCleanupService(
        FileService.Infrastructure.Storage.IUploadSessionRepository repo,
        FileService.Core.Interfaces.IFileStorageService storage,
        Microsoft.Extensions.Configuration.IConfiguration config,
        ILogger<UploadSessionCleanupService> logger)
    {
        _repo = repo;
        _storage = storage;
        _logger = logger;
        var minutes = config.GetValue<int>("Upload:Cleanup:IntervalMinutes", 60);
        _interval = TimeSpan.FromMinutes(Math.Max(1, minutes));
        _maxSessionsPerRun = config.GetValue<int>("Upload:Cleanup:MaxSessionsPerRun", 500);
        _retryCount = config.GetValue<int>("Upload:Cleanup:RetryCount", 3);
        _baseDelayMs = config.GetValue<int>("Upload:Cleanup:BaseDelayMs", 200);
        _maxDelayMs = config.GetValue<int>("Upload:Cleanup:MaxDelayMs", 5000);
        _enableBlockListCleanup = config.GetValue<bool>("Upload:Cleanup:EnableBlockListCleanup", false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("UploadSessionCleanupService started, interval: {Interval}", _interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during upload session cleanup");
            }
            await Task.Delay(_interval, stoppingToken);
        }
        _logger.LogInformation("UploadSessionCleanupService stopping");
    }

    // Made public for testing. Processes up to _maxSessionsPerRun expired sessions.
    public async Task CleanupOnceAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var processed = 0;
        await foreach (var session in _repo.QueryExpiredAsync(now, _maxSessionsPerRun, ct))
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var blobPath = session.RowKey;
                _logger.LogInformation("Cleaning expired upload session: {BlobPath}", blobPath);
                var succeeded = false;
                for (var attempt = 0; attempt < _retryCount; attempt++)
                {
                    try
                    {
                        await _storage.AbortUploadAsync(blobPath, ct);
                        succeeded = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Attempt {Attempt} failed aborting blob {BlobPath}", attempt + 1, blobPath);
                        if (attempt == _retryCount - 1) break;
                        var delay = Math.Min(_maxDelayMs, _baseDelayMs * (int)Math.Pow(2, attempt));
                        var jitter = new Random().Next(0, delay);
                        await Task.Delay(jitter, ct);
                    }
                }

                if (!succeeded)
                {
                    _logger.LogWarning("Failed to abort blob {BlobPath} after {RetryCount} attempts", blobPath, _retryCount);
                    // Optionally try deeper cleanup if enabled
                    if (_enableBlockListCleanup)
                    {
                        try
                        {
                            _logger.LogInformation("Block-list cleanup enabled. Attempting deeper cleanup for {BlobPath}", blobPath);
                            // Attempt one extra Abort to cover eventual consistency, don't block on errors.
                            await _storage.AbortUploadAsync(blobPath, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Deep cleanup failed for {BlobPath}", blobPath);
                        }
                    }
                }

                // Try to delete session row (with retries)
                var deleted = false;
                for (var attempt = 0; attempt < _retryCount; attempt++)
                {
                    try
                    {
                        await _repo.DeleteAsync(blobPath, ct);
                        deleted = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Attempt {Attempt} failed deleting session {BlobPath}", attempt + 1, blobPath);
                        if (attempt == _retryCount - 1) break;
                        var delay = Math.Min(_maxDelayMs, _baseDelayMs * (int)Math.Pow(2, attempt));
                        var jitter = new Random().Next(0, delay);
                        await Task.Delay(jitter, ct);
                    }
                }

                if (!deleted)
                {
                    _logger.LogWarning("Failed to delete session record {BlobPath} after {RetryCount} attempts", blobPath, _retryCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning session record");
            }
            processed++;
            if (processed >= _maxSessionsPerRun) break;
        }
    }
}
