using FileService.Core.Interfaces;
using FileService.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Services
var useEf = builder.Configuration.GetValue("Persistence:UseEf", true);
// Force in-memory for development to avoid database hanging issues
var isDevelopment = builder.Environment.IsDevelopment();
if (isDevelopment)
{
    Console.WriteLine("[STARTUP] Using in-memory repository for development mode");
    builder.Services.AddSingleton<IFileMetadataRepository, InMemoryFileMetadataRepository>();
}
else if (useEf)
{
    // Support SQL Server in staging/production when configured, otherwise fall back to SQLite
    var useSqlServer = builder.Configuration.GetValue("Persistence:UseSqlServer", false);
    if (useSqlServer)
    {
        var sqlConn = builder.Configuration.GetValue<string>("Sql__ConnectionString")
                      ?? builder.Configuration.GetValue<string>("Persistence:SqlConnectionString");
        if (!string.IsNullOrWhiteSpace(sqlConn))
        {
            builder.Services.AddDbContext<FileService.Infrastructure.Data.FileServiceDbContext>(opt =>
                opt.UseSqlServer(sqlConn));
            builder.Services.AddScoped<IFileMetadataRepository, FileService.Infrastructure.Data.EfFileMetadataRepository>();
        }
        else
        {
            // Fallback to SQLite if SQL connection isn't provided
            var dbPath = builder.Configuration.GetValue<string>("Persistence:SqlitePath") ?? Path.Combine(AppContext.BaseDirectory, "files.db");
            builder.Services.AddDbContext<FileService.Infrastructure.Data.FileServiceDbContext>(opt =>
                opt.UseSqlite($"Data Source={dbPath}"));
            builder.Services.AddScoped<IFileMetadataRepository, FileService.Infrastructure.Data.EfFileMetadataRepository>();
        }
    }
    else
    {
        var dbPath = builder.Configuration.GetValue<string>("Persistence:SqlitePath") ?? Path.Combine(AppContext.BaseDirectory, "files.db");
        builder.Services.AddDbContext<FileService.Infrastructure.Data.FileServiceDbContext>(opt =>
            opt.UseSqlite($"Data Source={dbPath}"));
        builder.Services.AddScoped<IFileMetadataRepository, FileService.Infrastructure.Data.EfFileMetadataRepository>();
    }
}
else
{
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

// No authentication: the service runs without requiring special header-based authentication or tokens.

var app = builder.Build();

var envMode = builder.Configuration.GetValue<string>("EnvironmentMode") ?? builder.Environment.EnvironmentName;
var isDevMode = envMode.Equals("Development", StringComparison.OrdinalIgnoreCase);
// Apply pending migrations only if configured (dev convenience). For production you may set AutoMigrate=false and run migrations explicitly.
if (useEf && !isDevelopment && builder.Configuration.GetValue("Persistence:AutoMigrate", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FileService.Infrastructure.Data.FileServiceDbContext>();
    try { db.Database.Migrate(); }
    catch (Exception ex) { app.Logger.LogError(ex, "[DB MIGRATE] Failed"); }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();

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

        if (file.Length > 50 * 1024 * 1024)
            return Results.BadRequest("File too large (50 MB limit)");

        // No user context: store blobs without a user prefix.
        var blobPath = $"{Guid.NewGuid()}_{file.FileName}";
        await using var stream = file.OpenReadStream();
        await storage.UploadAsync(blobPath, stream, file.ContentType, ct);

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
        Console.WriteLine($"[UPLOAD ERROR] {ex}");
        return Results.Problem($"Upload failed: {ex.Message}");
    }
}).DisableAntiforgery();

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
    app.Logger.LogInformation("[DELETE] Deleting file {Id} from storage and repository", id);
    await storage.DeleteAsync(rec.BlobPath, ct);
    await repo.DeleteAsync(id, ct);
    app.Logger.LogInformation("[DELETE] Successfully deleted file {Id}", id);
    return Results.NoContent();
});

app.Run();

// Authentication removed: no external header-based user context is used.

// Expose Program for WebApplicationFactory in tests
public partial class Program { }
