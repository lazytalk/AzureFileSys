using FileService.Api.Models;
using FileService.Api.Services;
using FileService.Core.Interfaces;
using FileService.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;

namespace FileService.Api.Endpoints;

/// <summary>
/// File Operations API endpoints for upload, download, list, and delete operations.
/// </summary>
public static class FileOperationEndpoints
{
    public static void MapFileOperationEndpoints(this WebApplication app)
    {
        app.MapPost("/api/files/begin-upload", BeginUploadHandler);
        app.MapPost("/api/files/complete-upload/{id:guid}", CompleteUploadHandler);
        app.MapGet("/api/files", ListFilesHandler);
        app.MapGet("/api/files/{id:guid}", GetFileHandler);
        app.MapDelete("/api/files/{id:guid}", DeleteFileHandler);
    }

    private static async Task<IResult> BeginUploadHandler(
        [FromBody] BeginUploadRequest request,
        PowerSchoolUserContext user,
        IFileStorageService storage,
        IFileMetadataRepository repo,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.FileName))
                return Results.BadRequest("FileName is required");

            // Limit file size check could be enforced here if we trust the client, 
            // but real enforcement happens at storage level or implementation detail.
            if (request.SizeBytes > 50 * 1024 * 1024)
                return Results.BadRequest("File too large (50 MB limit)");

            var fileId = Guid.NewGuid();
            // Naming convention: {userId}/{fileId}_{originalName}
            var blobPath = $"{user.UserId}/{fileId}_{request.FileName}";

            var record = new FileService.Core.Entities.FileRecord
            {
                Id = fileId,
                FileName = request.FileName,
                ContentType = request.ContentType ?? "application/octet-stream",
                SizeBytes = request.SizeBytes,
                OwnerUserId = user.UserId,
                BlobPath = blobPath,
                IsUploaded = false // Not yet available
            };
            
            await repo.AddAsync(record, ct);

            // Generate SAS URL for the client to upload directly
            var sasUrl = await storage.GetWriteSasUrlAsync(blobPath, TimeSpan.FromMinutes(15), ct);

            return Results.Ok(new { FileId = record.Id, UploadUrl = sasUrl });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BEGIN-UPLOAD ERROR] {ex}");
            return Results.Problem($"Failed to start upload: {ex.Message}");
        }
    }

    private static async Task<IResult> CompleteUploadHandler(
        Guid id,
        PowerSchoolUserContext user,
        IFileMetadataRepository repo,
        IFileStorageService storage,
        CancellationToken ct)
    {
        try
        {
            var rec = await repo.GetAsync(id, ct);
            if (rec == null) return Results.NotFound();

            if (!user.IsAdmin && !rec.OwnerUserId.Equals(user.UserId, StringComparison.OrdinalIgnoreCase))
                return Results.Forbid();

            // Verify Storage before finalizing
            var actualSize = await storage.GetBlobSizeAsync(rec.BlobPath, ct);
            if (actualSize == null)
            {
                Console.WriteLine($"[COMPLETE-UPLOAD ERROR] Blob for {id} not found at path {rec.BlobPath}");
                return Results.Problem("Upload verification failed: file content not found in storage.");
            }

            if (actualSize == 0)
            {
                Console.WriteLine($"[COMPLETE-UPLOAD ERROR] Blob for {id} found but has 0 bytes.");
                // We'll trust the user if they intended to upload 0 bytes? 
                // Usually not. But let's fail it.
                return Results.Problem("Upload verification failed: file content is empty (0 bytes).");
            }

            // Mark as uploaded and ensure size matches actual
            rec.SizeBytes = actualSize.Value;
            rec.IsUploaded = true;
            await repo.UpdateAsync(rec, ct);

            Console.WriteLine($"[COMPLETE-UPLOAD] File {id} marked as uploaded");
            return Results.Ok(new { rec.Id, rec.FileName, Status = "Available" });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[COMPLETE-UPLOAD ERROR] {ex}");
            return Results.Problem($"Failed to complete upload: {ex.Message}");
        }
    }

    private static async Task<IResult> ListFilesHandler(
        [FromQuery] bool? all,
        PowerSchoolUserContext user,
        IFileMetadataRepository repo,
        CancellationToken ct)
    {
        Console.WriteLine($"[LIST] User ID: '{user.UserId}', IsAdmin: {user.IsAdmin}, All: {all}");
        
        if (string.IsNullOrWhiteSpace(user.UserId))
        {
            Console.WriteLine("[LIST ERROR] User ID is null or empty");
            return Results.BadRequest("User ID is required");
        }
        
        try
        {
            var includeAll = all.GetValueOrDefault(false);
            var list = includeAll && user.IsAdmin
                ? await repo.ListAllAsync(take: 100, ct: ct)
                : await repo.ListByOwnerAsync(user.UserId, ct);
            Console.WriteLine($"[LIST] Found {list.Count} files for user");
            
            var result = list.Select(f => new FileListItemDto(f.Id, f.FileName, f.SizeBytes, f.ContentType, f.UploadedAt, f.OwnerUserId));
            return Results.Ok(result);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LIST ERROR] {ex}");
            return Results.Problem($"List failed: {ex.Message}");
        }
    }

    private static async Task<IResult> GetFileHandler(
        Guid id,
        PowerSchoolUserContext user,
        IFileMetadataRepository repo,
        IFileStorageService storage,
        CancellationToken ct)
    {
        Console.WriteLine($"[GET] Looking for file ID: {id}, User: '{user.UserId}'");
        var rec = await repo.GetAsync(id, ct);
        if (rec == null)
        {
            Console.WriteLine($"[GET] File {id} not found in repository");
            return Results.NotFound();
        }
        
        Console.WriteLine($"[GET] Found file {id}, owner: '{rec.OwnerUserId}', user: '{user.UserId}', isAdmin: {user.IsAdmin}");
        if (!user.IsAdmin && !rec.OwnerUserId.Equals(user.UserId, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[GET] Access denied for file {id}");
            return Results.Forbid();
        }

        // For now return a pseudo SAS URL (or inline content?). We'll issue stub SAS URL.
        var sas = await storage.GetReadSasUrlAsync(rec.BlobPath, TimeSpan.FromMinutes(15), ct);
        Console.WriteLine($"[GET] Returning file details for {id}");
        return Results.Ok(new { rec.Id, rec.FileName, rec.ContentType, rec.SizeBytes, DownloadUrl = sas });
    }

    private static async Task<IResult> DeleteFileHandler(
        Guid id,
        PowerSchoolUserContext user,
        IFileMetadataRepository repo,
        IFileStorageService storage,
        CancellationToken ct)
    {
        Console.WriteLine($"[DELETE] Looking for file ID: {id}, User: '{user.UserId}'");
        var rec = await repo.GetAsync(id, ct);
        if (rec == null)
        {
            Console.WriteLine($"[DELETE] File {id} not found in repository");
            return Results.NotFound();
        }
        
        Console.WriteLine($"[DELETE] Found file {id}, owner: '{rec.OwnerUserId}', user: '{user.UserId}', isAdmin: {user.IsAdmin}");
        if (!user.IsAdmin && !rec.OwnerUserId.Equals(user.UserId, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[DELETE] Access denied for file {id}");
            return Results.Forbid();
        }
        
        Console.WriteLine($"[DELETE] Deleting file {id} from storage and repository");
        await storage.DeleteAsync(rec.BlobPath, ct);
        await repo.DeleteAsync(id, ct);
        Console.WriteLine($"[DELETE] Successfully deleted file {id}");
        return Results.NoContent();
    }
}
