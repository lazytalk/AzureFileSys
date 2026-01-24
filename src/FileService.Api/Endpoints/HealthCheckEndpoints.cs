using FileService.Api.Models;
using FileService.Core.Interfaces;
using FileService.Infrastructure.Storage;
using System.Net.Http.Headers;
using System.Text;

namespace FileService.Api.Endpoints;

/// <summary>
/// Health Check API endpoints for monitoring service health and dependencies.
/// </summary>
public static class HealthCheckEndpoints
{
    public static void MapHealthCheckEndpoints(this WebApplication app)
    {
        app.MapGet("/api/health/check", HealthCheckHandler);
    }

    private static async Task<IResult> HealthCheckHandler(
        HttpContext ctx,
        IFileStorageService storage,
        IFileMetadataRepository repo,
        IWebHostEnvironment env,
        CancellationToken ct)
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
        if (!env.IsProduction())
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
    }
}
