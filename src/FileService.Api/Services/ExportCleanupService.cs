using FileService.Core.Interfaces;

namespace FileService.Api.Services;

/// <summary>
/// Background service that periodically sweeps and deletes exported zip files older than 2 hours.
/// Runs every 10 minutes to clean up temporary export artifacts and save storage costs.
/// </summary>
public class ExportCleanupService : Microsoft.Extensions.Hosting.BackgroundService
{
    private readonly IFileStorageService _storage;
    private readonly ILogger<ExportCleanupService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(10);

    public ExportCleanupService(
        IFileStorageService storage,
        ILogger<ExportCleanupService> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var cutoff = DateTimeOffset.UtcNow.AddHours(-2);
            try
            {
                var items = await _storage.ListAsync("exports/", stoppingToken);
                foreach (var item in items)
                {
                    if (item.LastModified.HasValue && item.LastModified.Value < cutoff)
                    {
                        try
                        {
                            await _storage.DeleteAsync(item.Path, stoppingToken);
                            _logger.LogInformation("[ExportCleanup] Deleted expired export {Path}", item.Path);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "[ExportCleanup] Failed to delete {Path}", item.Path);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ExportCleanup] Sweep failed");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (TaskCanceledException) { }
        }
    }
}
