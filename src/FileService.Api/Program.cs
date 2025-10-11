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

// Simple PowerSchool auth stub middleware registration
builder.Services.AddScoped<PowerSchoolUserContext>();

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

app.Use(async (ctx, next) =>
{
    var userCtx = ctx.RequestServices.GetRequiredService<PowerSchoolUserContext>();
    // Dev shortcut: allow ?devUser=xxx
    if (isDevMode && ctx.Request.Query.TryGetValue("devUser", out var devUser))
    {
        userCtx.UserId = devUser!;
        userCtx.Role = ctx.Request.Query.TryGetValue("role", out var r) ? r.ToString() : "user";
        await next();
        return;
    }

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

// Map Endpoints (initial version; can be moved to controllers or Minimal APIs kept)
app.MapPost("/api/files/upload", async (
    HttpRequest request,
    PowerSchoolUserContext user,
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

        var blobPath = $"{user.UserId}/{Guid.NewGuid()}_{file.FileName}";
        await using var stream = file.OpenReadStream();
        await storage.UploadAsync(blobPath, stream, file.ContentType, ct);

        var record = new FileService.Core.Entities.FileRecord
        {
            FileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            OwnerUserId = user.UserId,
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
    [FromQuery] bool all,
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
        var list = all && user.IsAdmin
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

app.Run();

public class PowerSchoolUserContext
{
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public bool IsAdmin => Role.Equals("admin", StringComparison.OrdinalIgnoreCase);
}
