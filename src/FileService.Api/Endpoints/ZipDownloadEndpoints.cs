using FileService.Api.Models;
using FileService.Api.Services;
using FileService.Core.Interfaces;
using FileService.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;

namespace FileService.Api.Endpoints;

/// <summary>
/// Async Batch Download API endpoints for creating and managing zip file downloads.
/// </summary>
public static class ZipDownloadEndpoints
{
    // Shared in-memory job tracker for zip generation
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, ZipJobStatus> ZipJobs = 
        new System.Collections.Concurrent.ConcurrentDictionary<Guid, ZipJobStatus>();

    public static void MapZipDownloadEndpoints(this WebApplication app)
    {
        app.MapPost("/api/files/download-zip", StartZipDownloadHandler);
        app.MapGet("/api/files/download-zip/{jobId:guid}", GetZipJobStatusHandler);
        app.MapDelete("/api/files/download-zip/{jobId:guid}", CleanupZipJobHandler);
    }

    private static async Task<IResult> StartZipDownloadHandler(
        [FromBody] List<Guid> fileIds,
        PowerSchoolUserContext user,
        IFileMetadataRepository repo,
        IFileStorageService storage,
        CancellationToken ct)
    {
        if (fileIds == null || fileIds.Count == 0)
            return Results.BadRequest("No file IDs provided");
        
        // 1. Validate permissions synchronously
        var validRecords = new List<FileService.Core.Entities.FileRecord>();
        foreach(var id in fileIds)
        {
            var rec = await repo.GetAsync(id, ct);
            if (rec != null && (user.IsAdmin || rec.OwnerUserId.Equals(user.UserId, StringComparison.OrdinalIgnoreCase)))
            {
                validRecords.Add(rec);
            }
        }

        if (validRecords.Count == 0)
            return Results.NotFound("No valid files found to download");

        // 2. Start Background Job
        var jobId = Guid.NewGuid();
        ZipJobs[jobId] = new ZipJobStatus { Status = "Processing", Progress = "Started" };

        // Fire and forget (careful with scope - using singletons here so it's safer)
        _ = ProcessZipJobAsync(jobId, validRecords, storage);

        return Results.Accepted($"/api/files/download-zip/{jobId}", new { JobId = jobId, Status = "Processing" });
    }

    private static IResult GetZipJobStatusHandler(Guid jobId)
    {
        if (ZipJobs.TryGetValue(jobId, out var job))
            return Results.Ok(job);
        return Results.NotFound(new { Error = "Job not found" });
    }

    private static async Task<IResult> CleanupZipJobHandler(
        Guid jobId,
        IFileStorageService storage)
    {
        if (ZipJobs.TryRemove(jobId, out var job) && job.BlobPath != null)
        {
            try
            {
                await storage.DeleteAsync(job.BlobPath, CancellationToken.None);
                Console.WriteLine($"[ZIP-JOB] Cleaned up zip {jobId} on user request");
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ZIP-JOB] Cleanup failed: {ex.Message}");
                return Results.Problem($"Cleanup failed: {ex.Message}");
            }
        }
        return Results.NotFound();
    }

    private static async Task ProcessZipJobAsync(
        Guid jobId,
        List<FileService.Core.Entities.FileRecord> validRecords,
        IFileStorageService storage)
    {
        try 
        {
            Console.WriteLine($"[ZIP-JOB] Starting job {jobId} for {validRecords.Count} files");
            
            var zipBlobPath = $"exports/{jobId}.zip";
            
            // Streaming mode: Open a write stream to Azure Blob immediately.
            // This ensures we don't buffer the whole zip in RAM.
            using (var blobStream = await storage.OpenWriteAsync(zipBlobPath, "application/zip", CancellationToken.None))
            using (var archive = new System.IO.Compression.ZipArchive(blobStream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: false))
            {
                foreach (var rec in validRecords)
                {
                    try 
                    {
                        var entry = archive.CreateEntry(rec.FileName);
                        using var entryStream = entry.Open();
                        // Note: Using CancellationToken.None to avoid aborting if HTTP request cancels
                        using var sourceStream = await storage.DownloadAsync(rec.BlobPath, CancellationToken.None);
                        
                        if (sourceStream != null)
                            await sourceStream.CopyToAsync(entryStream, CancellationToken.None);
                        else 
                        {
                            using var w = new StreamWriter(entryStream);
                            await w.WriteAsync($"Error: Content missing for {rec.FileName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ZIP-JOB] File error: {ex.Message}");
                    }
                }
            } // Archive Dispose writes CD; BlobStream Dispose commits block list.
            
            // Get SAS for the exported zip
            var sasUrl = await storage.GetReadSasUrlAsync(zipBlobPath, TimeSpan.FromHours(1), CancellationToken.None);
            
            // Update Job
            if (ZipJobs.TryGetValue(jobId, out var job))
            {
                job.Status = "Completed";
                job.DownloadUrl = sasUrl;
                job.Progress = "Ready";
                job.BlobPath = zipBlobPath;
            }
            Console.WriteLine($"[ZIP-JOB] Job {jobId} completed");
            
            // Schedule auto-cleanup after 2 hours
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromHours(2));
                try
                {
                    await storage.DeleteAsync(zipBlobPath, CancellationToken.None);
                    ZipJobs.TryRemove(jobId, out _);
                    Console.WriteLine($"[ZIP-JOB] Auto-cleaned expired zip {jobId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ZIP-JOB] Cleanup error: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ZIP-JOB] Critical error: {ex}");
            if (ZipJobs.TryGetValue(jobId, out var job))
            {
                job.Status = "Failed";
                job.Error = ex.Message;
            }
        }
    }
}
