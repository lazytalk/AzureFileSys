using FileService.Core.Interfaces;
using FileService.Infrastructure.Storage;
using FileService.Infrastructure.Data;
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

// Simple PowerSchool auth stub middleware registration
builder.Services.AddScoped<PowerSchoolUserContext>();

var app = builder.Build();

var isDevMode = builder.Environment.IsDevelopment();

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();

// Configure default files (serves index.html when accessing root /)
app.UseDefaultFiles(new DefaultFilesOptions
{
    DefaultFileNames = new List<string> { "index.html" }
});
app.UseStaticFiles();

app.Use(async (ctx, next) =>
{
    // Skip authentication for Swagger, static files, and health checks
    if (ctx.Request.Path.StartsWithSegments("/swagger") || 
        ctx.Request.Path.StartsWithSegments("/_framework") ||
        ctx.Request.Path.StartsWithSegments("/_vs") ||
        ctx.Request.Path.StartsWithSegments("/api/health") ||
        ctx.Request.Path.StartsWithSegments("/dev/powerschool") ||
        ctx.Request.Path.Value?.EndsWith(".html") == true ||
        ctx.Request.Path.Value?.EndsWith(".css") == true ||
        ctx.Request.Path.Value?.EndsWith(".js") == true)
    {
        await next();
        return;
    }

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

    // 1) Upload test file
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var env = ctx.RequestServices.GetRequiredService<IWebHostEnvironment>();
            var testFilePath = Path.Combine(env.WebRootPath ?? string.Empty, "health-test.txt");
            byte[] data = File.Exists(testFilePath)
                ? await File.ReadAllBytesAsync(testFilePath, ct)
                : Encoding.UTF8.GetBytes("health upload from server check\n");

            using var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(data);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            form.Add(fileContent, "file", "health-test.txt");

            var response = await http.PostAsync($"{baseUrl}/api/files/upload", form, ct);
            sw.Stop();
            var status = MapStatus(response.StatusCode);

            try
            {
                // Try read id from JSON body
                var body = await response.Content.ReadAsStringAsync(ct);
                if (!string.IsNullOrWhiteSpace(body))
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("id", out var idEl))
                        createdId = idEl.GetString();
                    else if (doc.RootElement.TryGetProperty("Id", out var idEl2))
                        createdId = idEl2.GetString();
                }
                // Fallback: parse Location header
                if (string.IsNullOrEmpty(createdId) && response.Headers.Location != null)
                {
                    var seg = response.Headers.Location.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                    if (!string.IsNullOrWhiteSpace(seg)) createdId = seg;
                }
            }
            catch { /* ignore parse issues */ }

            checks.Add(new { name = "Upload File", status, message = $"{response.StatusCode} ({(int)response.StatusCode})", responseTime = sw.ElapsedMilliseconds });
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

async Task<object> TestEndpoint(string name, string method, string url, HttpContext ctx, CancellationToken ct)
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(5);
        
        // Add dev user to bypass auth
        http.DefaultRequestHeaders.Add("X-PowerSchool-User", "healthcheck");
        http.DefaultRequestHeaders.Add("X-PowerSchool-Role", "admin");
        
        HttpResponseMessage response;
        if (method == "POST" && url.EndsWith("/api/files/upload", StringComparison.OrdinalIgnoreCase))
        {
            // Compose multipart/form-data with a small test file from wwwroot
            var env = ctx.RequestServices.GetRequiredService<IWebHostEnvironment>();
            var testFilePath = Path.Combine(env.WebRootPath ?? string.Empty, "health-test.txt");
            byte[] data;
            string fileName = "health-test.txt";
            if (!File.Exists(testFilePath))
            {
                data = Encoding.UTF8.GetBytes("health upload from server check\n");
            }
            else
            {
                data = await File.ReadAllBytesAsync(testFilePath, ct);
            }

            using var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(data);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            form.Add(fileContent, "file", fileName);
            response = await http.PostAsync(url, form, ct);
        }
        else if (method == "POST")
        {
            // Generic POST probe
            response = await http.PostAsync(url, new StringContent(string.Empty), ct);
        }
        else if (method == "GET")
        {
            response = await http.GetAsync(url, ct);
        }
        else if (method == "DELETE")
        {
            response = await http.DeleteAsync(url, ct);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported method: {method}");
        }
        
        sw.Stop();
        
        // Determine status based on response
        string status = response.StatusCode switch
        {
            System.Net.HttpStatusCode.OK => "healthy",
            System.Net.HttpStatusCode.Created => "healthy",
            System.Net.HttpStatusCode.Accepted => "healthy",
            System.Net.HttpStatusCode.NoContent => "healthy",
            System.Net.HttpStatusCode.NotFound => "healthy", // 404 is OK for test endpoints
            System.Net.HttpStatusCode.Forbidden or 
            System.Net.HttpStatusCode.Unauthorized => "warning", // Auth issues but service is up
            _ => "unhealthy"
        };
        
        return new 
        { 
            name,
            status,
            message = $"{response.StatusCode} ({(int)response.StatusCode})",
            responseTime = sw.ElapsedMilliseconds
        };
    }
    catch (HttpRequestException ex)
    {
        sw.Stop();
        return new { name, status = "unhealthy", message = $"Connection failed: {ex.Message}", responseTime = sw.ElapsedMilliseconds };
    }
    catch (OperationCanceledException)
    {
        sw.Stop();
        return new { name, status = "unhealthy", message = "Request timeout (>5s)", responseTime = sw.ElapsedMilliseconds };
    }
    catch (Exception ex)
    {
        sw.Stop();
        return new { name, status = "unhealthy", message = $"Error: {ex.Message}", responseTime = sw.ElapsedMilliseconds };
    }
}

// Map Endpoints (initial version; can be moved to controllers or Minimal APIs kept)
app.MapPost("/api/files/upload", async (
    [FromForm] IFormFile file,
    PowerSchoolUserContext user,
    IFileStorageService storage,
    IFileMetadataRepository repo,
    CancellationToken ct) =>
{
    try
    {
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

app.Run();

public class PowerSchoolUserContext
{
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public bool IsAdmin => Role.Equals("admin", StringComparison.OrdinalIgnoreCase);
}
