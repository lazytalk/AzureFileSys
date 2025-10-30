using FileService.Core.Interfaces;
using FileService.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
using FileService.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Services - Read all configuration from appsettings files
var useInMemory = builder.Configuration.GetValue("Persistence:UseInMemory", false);
var useTableStorage = builder.Configuration.GetValue("Persistence:UseTableStorage", false);
var useEf = builder.Configuration.GetValue("Persistence:UseEf", false);
var useSqlServer = builder.Configuration.GetValue("Persistence:UseSqlServer", false);

// Debug logging for configuration values
Console.WriteLine($"[STARTUP DEBUG] Environment: {builder.Environment.EnvironmentName}");
Console.WriteLine($"[STARTUP DEBUG] Persistence:UseInMemory = {useInMemory}");
Console.WriteLine($"[STARTUP DEBUG] Persistence:UseTableStorage = {useTableStorage}");
Console.WriteLine($"[STARTUP DEBUG] Persistence:UseEf = {useEf}");
Console.WriteLine($"[STARTUP DEBUG] Persistence:UseSqlServer = {useSqlServer}");

// Configure metadata repository based on configuration (not environment)
if (useInMemory)
{
    Console.WriteLine("[STARTUP] Using in-memory repository");
    builder.Services.AddSingleton<IFileMetadataRepository, InMemoryFileMetadataRepository>();
}
else if (useTableStorage)
{
    // Azure Table Storage configuration
    var tableConnString = builder.Configuration.GetValue<string>("TableStorage:ConnectionString")
                         ?? builder.Configuration.GetValue<string>("BlobStorage:ConnectionString"); // Reuse blob storage connection
    
    if (!string.IsNullOrWhiteSpace(tableConnString))
    {
        Console.WriteLine("[STARTUP] Using Azure Table Storage for metadata");
        builder.Services.AddSingleton(sp => new Azure.Data.Tables.TableServiceClient(tableConnString));
        builder.Services.AddSingleton<IFileMetadataRepository, FileService.Infrastructure.Storage.AzureTableFileMetadataRepository>();
    }
    else
    {
        Console.WriteLine("[STARTUP] Table Storage connection not found, falling back to in-memory repository");
        builder.Services.AddSingleton<IFileMetadataRepository, InMemoryFileMetadataRepository>();
    }
}
else if (useEf)
{
    // Entity Framework with SQL Server or SQLite
    if (useSqlServer)
    {
        var sqlConn = builder.Configuration.GetValue<string>("Sql:ConnectionString")
                      ?? builder.Configuration.GetValue<string>("Persistence:SqlConnectionString");
        if (!string.IsNullOrWhiteSpace(sqlConn))
        {
            Console.WriteLine("[STARTUP] Using Entity Framework with SQL Server");
            builder.Services.AddDbContext<FileService.Infrastructure.Data.FileServiceDbContext>(opt =>
                opt.UseSqlServer(sqlConn));
            builder.Services.AddScoped<IFileMetadataRepository, FileService.Infrastructure.Data.EfFileMetadataRepository>();
        }
        else
        {
            // Fallback to SQLite if SQL connection isn't provided
            Console.WriteLine("[STARTUP] SQL Server connection not found, falling back to SQLite");
            var dbPath = builder.Configuration.GetValue<string>("Persistence:SqlitePath") ?? Path.Combine(AppContext.BaseDirectory, "files.db");
            builder.Services.AddDbContext<FileService.Infrastructure.Data.FileServiceDbContext>(opt =>
                opt.UseSqlite($"Data Source={dbPath}"));
            builder.Services.AddScoped<IFileMetadataRepository, FileService.Infrastructure.Data.EfFileMetadataRepository>();
        }
    }
    else
    {
        Console.WriteLine("[STARTUP] Using Entity Framework with SQLite");
        var dbPath = builder.Configuration.GetValue<string>("Persistence:SqlitePath") ?? Path.Combine(AppContext.BaseDirectory, "files.db");
        builder.Services.AddDbContext<FileService.Infrastructure.Data.FileServiceDbContext>(opt =>
            opt.UseSqlite($"Data Source={dbPath}"));
        builder.Services.AddScoped<IFileMetadataRepository, FileService.Infrastructure.Data.EfFileMetadataRepository>();
    }
}
else
{
    Console.WriteLine("[STARTUP] No persistence configuration found, defaulting to in-memory repository");
    builder.Services.AddSingleton<IFileMetadataRepository, InMemoryFileMetadataRepository>();
}
builder.Services.Configure<FileService.Infrastructure.Storage.BlobStorageOptions>(builder.Configuration.GetSection("BlobStorage"));
// Conditional registration: if BlobStorage:UseLocalStub true OR no connection string, use stub
builder.Services.AddSingleton<IFileStorageService>(sp =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FileService.Infrastructure.Storage.BlobStorageOptions>>().Value;
    if (opts.UseLocalStub || string.IsNullOrWhiteSpace(opts.ConnectionString))
        return new StubBlobFileStorageService();
    return new AzureBlobFileStorageService(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FileService.Infrastructure.Storage.BlobStorageOptions>>());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddHttpClient();

// SignalR for upload progress notifications
builder.Services.AddSignalR();
// Register cleanup hosted service
builder.Services.AddHostedService<FileService.Api.Services.UploadSessionCleanupService>();

// Register UploadSessionRepository for persistence of resumable sessions (via interface)
builder.Services.AddSingleton<FileService.Infrastructure.Storage.IUploadSessionRepository>(sp =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FileService.Infrastructure.Storage.BlobStorageOptions>>().Value;
    return new FileService.Infrastructure.Storage.UploadSessionRepository(opts);
});

// Configure CORS
var enableCors = builder.Configuration.GetValue("Features:EnableCors", false);
if (enableCors)
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "*" };
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            if (allowedOrigins.Contains("*"))
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            }
            else
            {
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            }
        });
    });
    Console.WriteLine($"[STARTUP] CORS enabled with origins: {string.Join(", ", allowedOrigins)}");
}
else
{
    Console.WriteLine("[STARTUP] CORS is disabled");
}

// No authentication: the service runs without requiring special header-based authentication or tokens.

var app = builder.Build();

static string _formatBytes(long bytes)
{
    if (bytes >= 1024 * 1024) return $"{Math.Round(bytes / (1024.0 * 1024.0), 2)} MB";
    if (bytes >= 1024) return $"{Math.Round(bytes / 1024.0, 2)} KB";
    return $"{bytes} B";
}

// Upload concurrency limiter and in-memory progress tracking for resumable uploads
var maxConcurrentUploads = builder.Configuration.GetValue<int>("BlobStorage:MaxConcurrentUploads", 8);
var uploadSemaphore = new System.Threading.SemaphoreSlim(maxConcurrentUploads);
var uploadProgress = new System.Collections.Concurrent.ConcurrentDictionary<string, long>(); // bytes uploaded
var uploadCommitted = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>();

// API key for upload hardening (optional). If set, requests must include X-Api-Key header.
var uploadApiKey = builder.Configuration.GetValue<string>("Upload:ApiKey");

// Simple middleware to enforce API key for upload endpoints
app.Use(async (context, next) =>
{
    try
    {
        if (!string.IsNullOrEmpty(uploadApiKey) && context.Request.Path.StartsWithSegments("/api/files/upload"))
        {
            if (!context.Request.Headers.TryGetValue("X-Api-Key", out var provided) || provided != uploadApiKey)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Unauthorized");
                return;
            }
        }
    }
    catch { }
    await next();
});

var envMode = builder.Configuration.GetValue<string>("EnvironmentMode") ?? builder.Environment.EnvironmentName;
var isDevMode = envMode.Equals("Development", StringComparison.OrdinalIgnoreCase);
// Apply pending migrations only if configured and AutoMigrate is enabled
if (useEf && builder.Configuration.GetValue("Persistence:AutoMigrate", false))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FileService.Infrastructure.Data.FileServiceDbContext>();
    try { db.Database.Migrate(); }
    catch (Exception ex) { app.Logger.LogError(ex, "[DB MIGRATE] Failed"); }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();

// Use CORS middleware if enabled
if (enableCors)
{
    app.UseCors();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();
app.MapControllers();

// Debug endpoint to inspect resolved environment settings
app.MapGet("/debug/config", (IConfiguration configuration, Microsoft.Extensions.Hosting.IHostEnvironment hostEnv) =>
{
    var envMode = configuration.GetValue<string>("EnvironmentMode");
    var resolved = string.IsNullOrEmpty(envMode) ? hostEnv.EnvironmentName : envMode;
    return Results.Ok(new { EnvironmentMode_Config = envMode, HostEnvironment = hostEnv.EnvironmentName, Resolved = resolved });
});

// No dev token endpoints; the application runs without authentication in all environments if configured.

// Map Endpoints (initial version; can be moved to controllers or Minimal APIs kept)
app.MapPost("/api/files/upload", async (
    HttpRequest request,
    IFileStorageService storage,
    IFileMetadataRepository repo,
    IOptions<FileService.Infrastructure.Storage.BlobStorageOptions> blobOptions,
    CancellationToken ct) =>
{
    try
    {
        if (!request.HasFormContentType)
            return Results.BadRequest("Form data expected");

        var form = await request.ReadFormAsync(ct);
        var file = form.Files.FirstOrDefault();
        if (file == null || file.Length == 0)
            return Results.BadRequest("No file provided");

        // Use configurable max file size from BlobStorageOptions
        var maxFileSize = blobOptions.Value.MaxFileSizeBytes;
        if (file.Length > maxFileSize)
            return Results.BadRequest($"File too large. Maximum size: {maxFileSize / (1024 * 1024)} MB");

        app.Logger.LogInformation(
            "[UPLOAD] Starting upload: {FileName}, Size: {Size} bytes, ContentType: {ContentType}", 
            file.FileName, file.Length, file.ContentType);

        // No user context: store blobs without a user prefix.
        var blobPath = $"{Guid.NewGuid()}_{file.FileName}";
        
        // Stream upload with optimized chunking and parallelism
        await using var stream = file.OpenReadStream();
        var startTime = DateTime.UtcNow;
        await storage.UploadAsync(blobPath, stream, file.ContentType, ct);
        var duration = DateTime.UtcNow - startTime;
        
        app.Logger.LogInformation(
            "[UPLOAD] Completed upload: {FileName} in {Duration}ms, Speed: {Speed} MB/s",
            file.FileName, duration.TotalMilliseconds, 
            Math.Round((file.Length / (1024.0 * 1024.0)) / duration.TotalSeconds, 2));

        var record = new FileService.Core.Entities.FileRecord
        {
            FileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            OwnerUserId = string.Empty,
            BlobPath = blobPath
        };
        await repo.AddAsync(record, ct);

        return Results.Created($"/api/files/{record.Id}", new { record.Id, record.FileName });
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "[UPLOAD ERROR] Failed to upload file");
        return Results.Problem($"Upload failed: {ex.Message}");
    }
}).DisableAntiforgery();

// Resumable upload endpoints
app.MapPost("/api/files/upload/start", async (HttpRequest request, IUploadSessionRepository sessionRepo) =>
{
    using var doc = await System.Text.Json.JsonDocument.ParseAsync(request.Body);
    var root = doc.RootElement;
    var fileName = root.TryGetProperty("fileName", out var fn) ? fn.GetString() ?? "upload.bin" : "upload.bin";
    var contentType = root.TryGetProperty("contentType", out var ct) ? ct.GetString() ?? "application/octet-stream" : "application/octet-stream";
    var totalBytes = root.TryGetProperty("totalBytes", out var tb) ? tb.GetInt64() : 0L;
    var blobPath = $"{Guid.NewGuid()}_{fileName}";
    
    // Persist session to repository for cleanup service
    await sessionRepo.CreateAsync(blobPath, fileName, contentType, totalBytes);
    
    // initialize progress
    uploadProgress[blobPath] = 0;
    uploadCommitted[blobPath] = false;
    return Results.Ok(new { blobPath, fileName, contentType });
});

app.MapPut("/api/files/upload/{blobPath}/block/{blockId}", async (
    string blobPath,
    string blockId,
    HttpRequest request,
    IFileStorageService storage,
    IUploadSessionRepository sessionRepo, 
    IOptions<FileService.Infrastructure.Storage.BlobStorageOptions> blobOpts,
    CancellationToken ct) =>
{
    // Enforce concurrency limit per upload with 5-minute timeout
    if (!await uploadSemaphore.WaitAsync(TimeSpan.FromMinutes(5), ct))
    {
        return Results.StatusCode(503); // Service Unavailable - too many concurrent uploads
    }
    try
    {
        long bytesRead = 0;
        using var ms = new MemoryStream();
        await request.Body.CopyToAsync(ms);
        ms.Position = 0;
        bytesRead = ms.Length;

        // Validate Content-Range header if present
        if (request.Headers.TryGetValue("Content-Range", out var contentRange))
        {
            // Expected format: bytes start-end/total
            var cr = contentRange.ToString();
            try
            {
                var parts = cr.Split(' '); // ["bytes", "start-end/total"]
                if (parts.Length == 2 && parts[0].Equals("bytes", StringComparison.OrdinalIgnoreCase))
                {
                    var rangeParts = parts[1].Split('/');
                    var startEnd = rangeParts[0].Split('-');
                    var start = long.Parse(startEnd[0]);
                    var end = long.Parse(startEnd[1]);
                    var expectedLen = end - start + 1;
                    if (expectedLen != bytesRead)
                        return Results.BadRequest($"Content-Range length mismatch: expected {expectedLen}, got {bytesRead}");
                }
            }
            catch
            {
                return Results.BadRequest("Invalid Content-Range header");
            }
        }

        // Validate block size against configured MaximumTransferSizeBytes
        var maxBlock = blobOpts.Value.MaximumTransferSizeBytes ?? 4 * 1024 * 1024;
        if (bytesRead > maxBlock)
            return Results.BadRequest($"Block size too large. Maximum {_formatBytes(maxBlock)} allowed");

        ms.Position = 0;
        await storage.UploadBlockAsync(blobPath, blockId, ms);
        uploadProgress.AddOrUpdate(blobPath, bytesRead, (k, v) => v + bytesRead);
        // persist uploaded bytes in table storage
        await sessionRepo.AddUploadedBytesAsync(blobPath, bytesRead);

        // notify SignalR clients about progress (grouped by blobPath)
        try
        {
            var hub = app.Services.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<UploadProgressHub>>();
            var uploaded = uploadProgress.TryGetValue(blobPath, out var up) ? up : 0L;
            await hub.Clients.Group(blobPath).SendAsync("progress", new { uploaded, total = (long?)null, committed = false });
        }
        catch { /* non-fatal: continue if SignalR not available */ }

        return Results.Ok();
    }
    finally
    {
        uploadSemaphore.Release();
    }
});

app.MapPost("/api/files/upload/{blobPath}/commit", async (
    string blobPath,
    HttpRequest request,
    IFileStorageService storage,
    IFileMetadataRepository repo,
    IUploadSessionRepository sessionRepo,
    CancellationToken ct) =>
{
    using var doc = await System.Text.Json.JsonDocument.ParseAsync(request.Body, cancellationToken: ct);
    var root = doc.RootElement;
    
    if (!root.TryGetProperty("blockIds", out var blockIdsElement) || blockIdsElement.ValueKind != System.Text.Json.JsonValueKind.Array)
        return Results.BadRequest("blockIds array required");
    
    var ids = new List<string>();
    foreach (var item in blockIdsElement.EnumerateArray()) 
        ids.Add(item.GetString() ?? string.Empty);
    
    var contentType = root.TryGetProperty("contentType", out var ct_elem) ? ct_elem.GetString() ?? "application/octet-stream" : "application/octet-stream";
    var fileName = root.TryGetProperty("fileName", out var fn_elem) ? fn_elem.GetString() ?? blobPath : blobPath;

    await storage.CommitBlocksAsync(blobPath, ids, contentType, ct);
    uploadCommitted[blobPath] = true;
    
    // Mark session as committed in repository
    await sessionRepo.MarkCommittedAsync(blobPath, ct);

    // create metadata record
    var record = new FileService.Core.Entities.FileRecord
    {
        FileName = fileName,
        ContentType = contentType,
        SizeBytes = uploadProgress.TryGetValue(blobPath, out var bytes) ? bytes : 0,
        OwnerUserId = string.Empty,
        BlobPath = blobPath
    };
    await repo.AddAsync(record, ct);
    // notify SignalR clients about commit
    try
    {
        var hub = app.Services.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<UploadProgressHub>>();
        var uploaded = uploadProgress.TryGetValue(blobPath, out var up) ? up : 0L;
        await hub.Clients.Group(blobPath).SendAsync("progress", new { uploaded, total = record.SizeBytes, committed = true });
    }
    catch { }
    // cleanup progress tracking
    uploadProgress.TryRemove(blobPath, out _);

    return Results.Created($"/api/files/{record.Id}", new { record.Id, record.FileName });
});

app.MapPost("/api/files/upload/{blobPath}/abort", async (
    string blobPath,
    IFileStorageService storage,
    IUploadSessionRepository sessionRepo) =>
{
    await storage.AbortUploadAsync(blobPath);
    // Delete session from repository
    await sessionRepo.DeleteAsync(blobPath);
    uploadProgress.TryRemove(blobPath, out _);
    uploadCommitted.TryRemove(blobPath, out _);
    return Results.Ok();
});

// SSE progress endpoint
app.MapGet("/api/files/upload/{blobPath}/progress", async (string blobPath, HttpResponse response) =>
{
    response.ContentType = "text/event-stream";
    var ct = response.HttpContext.RequestAborted;
    try
    {
        while (!ct.IsCancellationRequested)
        {
            var bytes = uploadProgress.TryGetValue(blobPath, out var b) ? b : 0;
            var committed = uploadCommitted.TryGetValue(blobPath, out var c) ? c : false;
            var payload = System.Text.Json.JsonSerializer.Serialize(new { bytes, committed });
            await response.WriteAsync($"data: {payload}\n\n");
            await response.Body.FlushAsync(ct);
            if (committed) break;
            await Task.Delay(500, ct);
        }
    }
    catch (OperationCanceledException) { }
    return Results.Ok();
});
app.MapGet("/api/files", async (
    HttpRequest request,
    IFileMetadataRepository repo,
    CancellationToken ct) =>
{
    // Log request arrival for easier debugging
    app.Logger.LogInformation("[API] /api/files called from {RemoteIp}", request.HttpContext.Connection.RemoteIpAddress);
    // No authentication: return a (paginated) list of files. For now return up to 100 items.
    try
    {
        var list = await repo.ListAllAsync(take: 100, ct: ct);
    app.Logger.LogInformation("[LIST] Found {Count} files", list.Count);
        var result = list.Select(f => new FileService.Core.Models.FileListItemDto(f.Id, f.FileName, f.SizeBytes, f.ContentType, f.UploadedAt, f.OwnerUserId));
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "[LIST ERROR]");
        return Results.Problem($"List failed: {ex.Message}");
    }
});

app.MapGet("/api/files/{id:guid}", async (
    Guid id,
    IFileMetadataRepository repo,
    IFileStorageService storage,
    CancellationToken ct) =>
{
    app.Logger.LogInformation("[GET] Looking for file ID: {Id}", id);
    var rec = await repo.GetAsync(id, ct);
    if (rec == null)
    {
        app.Logger.LogWarning("[GET] File {Id} not found in repository", id);
        return Results.NotFound();
    }

    if (rec.IsDeleted)
    {
        app.Logger.LogInformation("[GET] File {Id} is soft-deleted", id);
        return Results.StatusCode(StatusCodes.Status410Gone);
    }

    // No access checks: return file details to any caller.
    var sas = await storage.GetReadSasUrlAsync(rec.BlobPath, TimeSpan.FromMinutes(15), ct);
    // If the storage returned an HTTP(S) URL (like an Azure SAS), return it directly.
    // For stub/local storage the returned "URL" may use a custom scheme (eg. stub://)
    // which browsers don't understand. In that case return a server-side download
    // endpoint that will stream the blob.
    string downloadUrl;
    if (Uri.TryCreate(sas, UriKind.Absolute, out var parsed) && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
    {
        downloadUrl = sas;
    }
    else
    {
        downloadUrl = $"/api/files/{rec.Id}/download";
    }
    app.Logger.LogInformation("[GET] Returning file details for {Id}", id);
    return Results.Ok(new { rec.Id, rec.FileName, rec.ContentType, rec.SizeBytes, DownloadUrl = downloadUrl });
});

// Server-side download endpoint that streams the blob content. This is used as a
// fall-back when storage provides a non-HTTP download URL (for example the
// StubBlobFileStorageService which returns stub:// URLs).
app.MapGet("/api/files/{id:guid}/download", async (
    Guid id,
    IFileMetadataRepository repo,
    IFileStorageService storage,
    CancellationToken ct) =>
{
    var rec = await repo.GetAsync(id, ct);
    if (rec == null) return Results.NotFound();
    if (rec.IsDeleted) return Results.StatusCode(StatusCodes.Status410Gone);
    var stream = await storage.DownloadAsync(rec.BlobPath, ct);
    if (stream == null) return Results.NotFound();
    // Return as an attachment so the browser will prompt to download
    return Results.File(stream, rec.ContentType ?? "application/octet-stream", rec.FileName);
});

app.MapDelete("/api/files/{id:guid}", async (
    Guid id,
    IFileMetadataRepository repo,
    IFileStorageService storage,
    CancellationToken ct) =>
{
    app.Logger.LogInformation("[DELETE] Looking for file ID: {Id}", id);
    var rec = await repo.GetAsync(id, ct);
    if (rec == null)
    {
        app.Logger.LogWarning("[DELETE] File {Id} not found in repository", id);
        return Results.NotFound();
    }

    // No access checks: allow deletion by any caller.
    app.Logger.LogInformation("[DELETE] Deleting file {Id} from storage and soft-deleting metadata", id);
    await storage.DeleteAsync(rec.BlobPath, ct);
    await repo.SoftDeleteAsync(id, ct);
    app.Logger.LogInformation("[DELETE] Successfully deleted file {Id}", id);
    return Results.NoContent();
});

// Admin: Clear all files (delete blobs, soft-delete metadata)
app.MapPost("/api/files/clear", async (
    IFileMetadataRepository repo,
    IFileStorageService storage,
    CancellationToken ct) =>
{
    var list = await repo.ListAllAsync(take: 10_000, skip: 0, ct: ct);
    int deleted = 0;
    foreach (var rec in list)
    {
        try
        {
            await storage.DeleteAsync(rec.BlobPath, ct);
            await repo.SoftDeleteAsync(rec.Id, ct);
            deleted++;
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "[CLEAR] Failed to delete {Id}", rec.Id);
        }
    }
    return Results.Ok(new { deleted });
}).WithTags("Admin");

// SignalR hub for upload progress
app.MapHub<UploadProgressHub>("/hubs/upload-progress");

app.Run();

// Authentication removed: no external header-based user context is used.

// Expose Program for WebApplicationFactory in tests
public partial class Program { }
