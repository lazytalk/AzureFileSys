using FileService.Core.Interfaces;
using FileService.Infrastructure.Storage;
using FileService.Infrastructure.Data;
using FileService.Api.Models;
using FileService.Api.Services;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Net.Http.Headers;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Services - Configure metadata persistence
var persistenceType = builder.Configuration.GetValue("Persistence:Type", "InMemory"); // InMemory, TableStorage
var isDevelopment = builder.Environment.IsDevelopment();

switch (persistenceType)
{
    case "TableStorage":
        var storageConnString = builder.Configuration.GetValue<string>("TableStorage:ConnectionString");
        if (string.IsNullOrWhiteSpace(storageConnString))
        {
            Console.WriteLine("[STARTUP ERROR] Table Storage connection string is missing!");
            throw new InvalidOperationException("TableStorage:ConnectionString is required when Persistence:Type=TableStorage");
        }
        Console.WriteLine("[STARTUP] Using Azure Table Storage for metadata");
        builder.Services.AddSingleton(new TableServiceClient(storageConnString));
        builder.Services.AddSingleton<IFileMetadataRepository>(sp =>
        {
            var tableService = sp.GetRequiredService<TableServiceClient>();
            var tableName = builder.Configuration.GetValue("TableStorage:TableName", "FileMetadata");
            return new TableStorageFileMetadataRepository(tableService, tableName);
        });
        break;
    
    case "InMemory":
    default:
        Console.WriteLine("[STARTUP] Using in-memory repository");
        builder.Services.AddSingleton<IFileMetadataRepository, InMemoryFileMetadataRepository>();
        break;
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

// CORS: allow origins via configuration
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalTools", policy =>
    {
        if (builder.Environment.IsDevelopment() || builder.Environment.IsStaging())
        {
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            if (corsOrigins.Length > 0)
            {
                policy.WithOrigins(corsOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            }
            else
            {
                policy.SetIsOriginAllowed(_ => false);
            }
        }
    });
});

// Simple PowerSchool auth stub middleware registration
builder.Services.AddScoped<PowerSchoolUserContext>();

// Background cleanup service for exported zip files
builder.Services.AddHostedService<ExportCleanupService>();

// OpenID Relying Party configuration for PowerSchool authentication
// Enable by default unless explicitly disabled
var enableOpenId = builder.Configuration.GetValue<bool>("OpenId:Enabled", true);
FileService.Api.Services.OpenIdRelyingPartyService? openIdService = null;
if (enableOpenId)
{
    var ipHostname = builder.Configuration.GetValue<string>("OpenId:Hostname") ?? 
                     builder.Configuration.GetValue<string>("OpenId:IpHostname") ?? 
                     "localhost";
    var port = builder.Configuration.GetValue<int>("OpenId:Port", 443);

    if (!string.IsNullOrEmpty(ipHostname))
    {
        openIdService = new FileService.Api.Services.OpenIdRelyingPartyService(ipHostname, port);
        Console.WriteLine($"[STARTUP] OpenID Relying Party enabled at https://{ipHostname}:{port}");
    }
    else
    {
        Console.WriteLine("[STARTUP WARNING] OpenID enabled but Hostname is not configured");
    }
}
else
{
    Console.WriteLine("[STARTUP] OpenID Relying Party disabled");
}

// Simple in-memory job tracker for zip generation
var zipJobs = new System.Collections.Concurrent.ConcurrentDictionary<Guid, ZipJobStatus>();

var app = builder.Build();

var isDevMode = builder.Environment.IsDevelopment();

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();

app.UseCors("LocalTools");

// Configure default files (serves index.html when accessing root /)
app.UseDefaultFiles(new DefaultFilesOptions
{
    DefaultFileNames = new List<string> { "index.html" }
});
app.UseStaticFiles();

// OpenID Relying Party endpoints (MUST be before terminal middleware)
if (openIdService != null)
{
    FileService.Api.Services.OpenIdRelyingPartyExtensions.MapOpenIdAuthentication(app, openIdService);
}

// Terminal middleware for PowerSchool authentication (MUST be after endpoint registration)
app.Use(async (ctx, next) =>
{
    var userCtx = ctx.RequestServices.GetRequiredService<PowerSchoolUserContext>();
    
    // Skip authentication for public OpenID endpoints
    if (ctx.Request.Path.StartsWithSegments("/authenticate") || ctx.Request.Path.StartsWithSegments("/verify"))
    {
        await next();
        return;
    }

    // Dev shortcut: allow ?devUser=xxx
    if (isDevMode && ctx.Request.Query.TryGetValue("devUser", out var devUser))
    {
        userCtx.UserId = devUser!;
        userCtx.Role = ctx.Request.Query.TryGetValue("role", out var r) ? r.ToString() : "user";
        await next();
        return;
    }

    // PowerSchool identity headers
    if (!ctx.Request.Headers.TryGetValue("X-PowerSchool-User", out var userHeader) || string.IsNullOrWhiteSpace(userHeader))
    {
        ctx.Response.StatusCode = 401;
        await ctx.Response.WriteAsJsonAsync(new { error = "Missing PowerSchool identity header 'X-PowerSchool-User'" });
        return;
    }
    var role = ctx.Request.Headers.TryGetValue("X-PowerSchool-Role", out var roleHeader) ? roleHeader.ToString() : "user";
    userCtx.UserId = userHeader!;
    userCtx.Role = role;
    await next();
});

if (isDevMode)
{
    app.MapPost("/dev/powerschool/token", (
        string userId,
        string role,
        string? secret
    ) =>
    {
        // Very simple token mimic: base64(userId|role|ticks|hmac)
        var ticks = DateTimeOffset.UtcNow.Ticks;
        var key = secret ?? "dev-shared-secret";
        var raw = $"{userId}|{role}|{ticks}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var sig = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(raw)));
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw + "|" + sig));
        return Results.Ok(new { token });
    });

    app.MapPost("/dev/powerschool/validate", (string token, string? secret) =>
    {
        try
        {
            var key = secret ?? "dev-shared-secret";
            var data = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parts = data.Split('|');
            if (parts.Length != 4) return Results.BadRequest("Malformed token");
            var user = parts[0];
            var role = parts[1];
            var ticks = parts[2];
            var sig = parts[3];
            var raw = $"{user}|{role}|{ticks}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(raw)));
            if (!expected.Equals(sig, StringComparison.OrdinalIgnoreCase)) return Results.Unauthorized();
            return Results.Ok(new { user, role });
        }
        catch
        {
            return Results.BadRequest("Invalid token format");
        }
    });
}

// Health Check Endpoint
app.MapGet("/api/health/check", async (HttpContext ctx, CancellationToken ct) =>
{
    var checks = new List<object>();
    var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";

    static string MapStatus(System.Net.HttpStatusCode code)
        => code switch
        {
            System.Net.HttpStatusCode.OK => "healthy",
            System.Net.HttpStatusCode.Created => "healthy",
            System.Net.HttpStatusCode.Accepted => "healthy",
            System.Net.HttpStatusCode.NoContent => "healthy",
            System.Net.HttpStatusCode.NotFound => "healthy",
            System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.Unauthorized => "warning",
            _ => "unhealthy"
        };

    // Create HttpClient with certificate validation bypass for self-signed certs
    var handler = new HttpClientHandler();
    // In non-production environments, allow self-signed certificates
    if (!builder.Environment.IsProduction())
    {
        handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    
    using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
    http.DefaultRequestHeaders.Add("X-PowerSchool-User", "healthcheck");
    http.DefaultRequestHeaders.Add("X-PowerSchool-Role", "admin");

    string? createdId = null;

    // 1) Upload test file (Direct-to-Blob Flow)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var env = ctx.RequestServices.GetRequiredService<IWebHostEnvironment>();
            var testFilePath = Path.Combine(env.WebRootPath ?? string.Empty, "health-test.txt");
            byte[] data = File.Exists(testFilePath)
                ? await File.ReadAllBytesAsync(testFilePath, ct)
                : Encoding.UTF8.GetBytes("health upload from server check\n");

            // A. Begin Upload
            var beginReq = new BeginUploadRequest 
            { 
                FileName = "health-test.txt", 
                SizeBytes = data.Length, 
                ContentType = "text/plain" 
            };
            var beginResp = await http.PostAsJsonAsync($"{baseUrl}/api/files/begin-upload", beginReq, ct);
            if (!beginResp.IsSuccessStatusCode) throw new Exception($"Begin upload failed: {beginResp.StatusCode}");
            
            var beginJson = await beginResp.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>(cancellationToken: ct);
            var fileId = beginJson?["fileId"]?.ToString();
            var uploadUrl = beginJson?["uploadUrl"]?.ToString();
            
            if (string.IsNullOrEmpty(fileId) || string.IsNullOrEmpty(uploadUrl))
                throw new Exception("Invalid start-upload response");

            createdId = fileId; 

            // B. Upload to Blob (PUT)
            // Note: If using Stub, URL has stub:// scheme. We must handle or mock it.
            if (uploadUrl.StartsWith("stub://"))
            {
                // In a real integration test, we might skip or have a special handler.
                // For now, we simulate success for stub.
            }
            else
            {
                // Create a separate client for Blob Storage (no auth headers, purely SAS)
                using var blobHttp = new HttpClient();
                blobHttp.Timeout = TimeSpan.FromSeconds(10);
                blobHttp.DefaultRequestHeaders.Add("x-ms-blob-type", "BlockBlob"); // Required for Azure Block Blob
                var putResp = await blobHttp.PutAsync(uploadUrl, new ByteArrayContent(data), ct);
                if (!putResp.IsSuccessStatusCode) 
                    throw new Exception($"Blob upload failed: {putResp.StatusCode}");
            }

            // C. Complete Upload
            var compResp = await http.PostAsync($"{baseUrl}/api/files/complete-upload/{fileId}", null, ct);
            if (!compResp.IsSuccessStatusCode) throw new Exception($"Complete upload failed: {compResp.StatusCode}");

            sw.Stop();
            checks.Add(new { name = "Upload File", status = "healthy", message = "OK (Direct-to-Blob)", responseTime = sw.ElapsedMilliseconds });
        }
        catch (Exception ex)
        {
            checks.Add(new { name = "Upload File", status = "unhealthy", message = ex.Message, responseTime = sw.ElapsedMilliseconds });
        }
    }

    // 2) List files
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await http.GetAsync($"{baseUrl}/api/files", ct);
            sw.Stop();
            checks.Add(new { name = "List Files", status = MapStatus(response.StatusCode), message = $"{response.StatusCode} ({(int)response.StatusCode})", responseTime = sw.ElapsedMilliseconds });
        }
        catch (Exception ex)
        {
            checks.Add(new { name = "List Files", status = "unhealthy", message = ex.Message, responseTime = sw.ElapsedMilliseconds });
        }
    }

    // 3) Get file (use created id when available)
    {
        var targetId = string.IsNullOrWhiteSpace(createdId) ? "00000000-0000-0000-0000-000000000000" : createdId;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await http.GetAsync($"{baseUrl}/api/files/{targetId}", ct);
            sw.Stop();
            checks.Add(new { name = "Get File", status = MapStatus(response.StatusCode), message = $"{response.StatusCode} ({(int)response.StatusCode})", responseTime = sw.ElapsedMilliseconds });
        }
        catch (Exception ex)
        {
            checks.Add(new { name = "Get File", status = "unhealthy", message = ex.Message, responseTime = sw.ElapsedMilliseconds });
        }
    }

    // 4) Delete file (use created id when available)
    {
        var targetId = string.IsNullOrWhiteSpace(createdId) ? "00000000-0000-0000-0000-000000000000" : createdId;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await http.DeleteAsync($"{baseUrl}/api/files/{targetId}", ct);
            sw.Stop();
            checks.Add(new { name = "Delete File", status = MapStatus(response.StatusCode), message = $"{response.StatusCode} ({(int)response.StatusCode})", responseTime = sw.ElapsedMilliseconds });
        }
        catch (Exception ex)
        {
            checks.Add(new { name = "Delete File", status = "unhealthy", message = ex.Message, responseTime = sw.ElapsedMilliseconds });
        }
    }

    // 5) Swagger endpoint
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await http.GetAsync($"{baseUrl}/swagger/index.html", ct);
            sw.Stop();
            checks.Add(new { name = "Swagger UI", status = MapStatus(response.StatusCode), message = $"{response.StatusCode} ({(int)response.StatusCode})", responseTime = sw.ElapsedMilliseconds });
        }
        catch (Exception ex)
        {
            checks.Add(new { name = "Swagger UI", status = "unhealthy", message = ex.Message, responseTime = sw.ElapsedMilliseconds });
        }
    }

    return Results.Ok(new { checks });
});

// Map Endpoints (initial version; can be moved to controllers or Minimal APIs kept)
app.MapPost("/api/files/begin-upload", async (
    [FromBody] BeginUploadRequest request,
    PowerSchoolUserContext user,
    IFileStorageService storage,
    IFileMetadataRepository repo,
    CancellationToken ct) =>
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
});

app.MapPost("/api/files/complete-upload/{id:guid}", async (
    Guid id,
    PowerSchoolUserContext user,
    IFileMetadataRepository repo,
    IFileStorageService storage,
    CancellationToken ct) =>
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
});

app.MapGet("/api/files", async (
    [FromQuery] bool? all,
    PowerSchoolUserContext user,
    IFileMetadataRepository repo,
    CancellationToken ct) =>
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
        
        var result = list.Select(f => new FileService.Core.Models.FileListItemDto(f.Id, f.FileName, f.SizeBytes, f.ContentType, f.UploadedAt, f.OwnerUserId));
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[LIST ERROR] {ex}");
        return Results.Problem($"List failed: {ex.Message}");
    }
});

app.MapGet("/api/files/{id:guid}", async (
    Guid id,
    PowerSchoolUserContext user,
    IFileMetadataRepository repo,
    IFileStorageService storage,
    CancellationToken ct) =>
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
});

app.MapDelete("/api/files/{id:guid}", async (
    Guid id,
    PowerSchoolUserContext user,
    IFileMetadataRepository repo,
    IFileStorageService storage,
    CancellationToken ct) =>
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
});

// Async Batch Download (Zip Job Start)
app.MapPost("/api/files/download-zip", async (
    [FromBody] List<Guid> fileIds,
    PowerSchoolUserContext user,
    IFileMetadataRepository repo,
    IFileStorageService storage,
    CancellationToken ct) =>
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
    zipJobs[jobId] = new ZipJobStatus { Status = "Processing", Progress = "Started" };

    // Fire and forget (careful with scope - using singletons here so it's safer)
    _ = Task.Run(async () =>
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
            if (zipJobs.TryGetValue(jobId, out var job))
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
                            zipJobs.TryRemove(jobId, out _);
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
            if (zipJobs.TryGetValue(jobId, out var job))
            {
                job.Status = "Failed";
                job.Error = ex.Message;
            }
        }
    });

    return Results.Accepted($"/api/files/download-zip/{jobId}", new { JobId = jobId, Status = "Processing" });
});

// Check Job Status
app.MapGet("/api/files/download-zip/{jobId:guid}", (Guid jobId) => 
{
    if (zipJobs.TryGetValue(jobId, out var job))
        return Results.Ok(job);
    return Results.NotFound(new { Error = "Job not found" });
});

// Cleanup Job (call after user downloads to save storage costs)
app.MapDelete("/api/files/download-zip/{jobId:guid}", async (
    Guid jobId,
    IFileStorageService storage) =>
{
    if (zipJobs.TryRemove(jobId, out var job) && job.BlobPath != null)
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
});

app.Run();
