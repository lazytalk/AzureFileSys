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
    var dbPath = builder.Configuration.GetValue<string>("Persistence:SqlitePath") ?? Path.Combine(AppContext.BaseDirectory, "files.db");
    builder.Services.AddDbContext<FileService.Infrastructure.Data.FileServiceDbContext>(opt =>
        opt.UseSqlite($"Data Source={dbPath}"));
    builder.Services.AddScoped<IFileMetadataRepository, FileService.Infrastructure.Data.EfFileMetadataRepository>();
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
    catch (Exception ex) { Console.WriteLine($"[DB MIGRATE] Failed: {ex.Message}"); }
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
    // No authentication: return a (paginated) list of files. For now return up to 100 items.
    try
    {
        var list = await repo.ListAllAsync(take: 100, ct: ct);
        Console.WriteLine($"[LIST] Found {list.Count} files");
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
    IFileMetadataRepository repo,
    IFileStorageService storage,
    CancellationToken ct) =>
{
    Console.WriteLine($"[GET] Looking for file ID: {id}");
    var rec = await repo.GetAsync(id, ct);
    if (rec == null)
    {
        Console.WriteLine($"[GET] File {id} not found in repository");
        return Results.NotFound();
    }

    // No access checks: return file details to any caller.
    var sas = await storage.GetReadSasUrlAsync(rec.BlobPath, TimeSpan.FromMinutes(15), ct);
    Console.WriteLine($"[GET] Returning file details for {id}");
    return Results.Ok(new { rec.Id, rec.FileName, rec.ContentType, rec.SizeBytes, DownloadUrl = sas });
});

app.MapDelete("/api/files/{id:guid}", async (
    Guid id,
    IFileMetadataRepository repo,
    IFileStorageService storage,
    CancellationToken ct) =>
{
    Console.WriteLine($"[DELETE] Looking for file ID: {id}");
    var rec = await repo.GetAsync(id, ct);
    if (rec == null)
    {
        Console.WriteLine($"[DELETE] File {id} not found in repository");
        return Results.NotFound();
    }

    // No access checks: allow deletion by any caller.
    Console.WriteLine($"[DELETE] Deleting file {id} from storage and repository");
    await storage.DeleteAsync(rec.BlobPath, ct);
    await repo.DeleteAsync(id, ct);
    Console.WriteLine($"[DELETE] Successfully deleted file {id}");
    return Results.NoContent();
});

app.Run();

// Authentication removed: no external header-based user context is used.

// Expose Program for WebApplicationFactory in tests
public partial class Program { }
