using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using FileService.Infrastructure.Storage;
using Xunit;

namespace FileService.Tests.Integration;

public class ResumableUploadTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ResumableUploadTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost") });
    }

    [Fact]
    public async Task ResumableUpload_StartBlockCommit_CreatesFileRecord()
    {
        // Arrange - Start session
        var startPayload = new
        {
            fileName = "large-file.bin",
            contentType = "application/octet-stream",
            totalBytes = 12 * 1024 * 1024 // 12MB total
        };
        var startResp = await _client.PostAsJsonAsync("/api/files/upload/start", startPayload);
        if (!startResp.IsSuccessStatusCode)
        {
            var error = await startResp.Content.ReadAsStringAsync();
            throw new Exception($"Start failed with {startResp.StatusCode}: {error}");
        }
        startResp.EnsureSuccessStatusCode();
        var session = await startResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(session);
        var blobPath = session["blobPath"];
        Assert.False(string.IsNullOrEmpty(blobPath));

        // Act - Upload 3 blocks (simulate 12MB file as 3x4MB chunks)
        var blockIds = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes($"block-{i:D6}"));
            blockIds.Add(blockId);
            var blockData = new byte[4 * 1024 * 1024]; // 4MB
            new Random(i).NextBytes(blockData); // Fill with deterministic random data
            var content = new ByteArrayContent(blockData);
            content.Headers.Add("Content-Range", $"bytes {i * 4194304}-{(i + 1) * 4194304 - 1}/12582912");
            
            var blockResp = await _client.PutAsync($"/api/files/upload/{blobPath}/block/{blockId}", content);
            Assert.True(blockResp.IsSuccessStatusCode, $"Block {i} upload failed: {await blockResp.Content.ReadAsStringAsync()}");
        }

        // Act - Commit blocks
        var commitPayload = new
        {
            blockIds,
            fileName = "large-file.bin",
            contentType = "application/octet-stream"
        };
        var commitResp = await _client.PostAsJsonAsync($"/api/files/upload/{blobPath}/commit", commitPayload);
        Assert.True(commitResp.IsSuccessStatusCode, $"Commit failed: {await commitResp.Content.ReadAsStringAsync()}");

        // Assert - Verify metadata record created
        var commitData = await commitResp.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Assert.NotNull(commitData);
        Assert.True(commitData.ContainsKey("id"));
        var fileId = commitData["id"].GetGuid();
        
        var fileResp = await _client.GetAsync($"/api/files/{fileId}");
        fileResp.EnsureSuccessStatusCode();
        var fileData = await fileResp.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Assert.NotNull(fileData);
        Assert.Equal("large-file.bin", fileData["fileName"].GetString());
        Assert.Equal(12582912, fileData["sizeBytes"].GetInt64());
    }

    [Fact]
    public async Task ResumableUpload_OversizedBlock_Returns400()
    {
        // Arrange - Start session
        var startResp = await _client.PostAsJsonAsync("/api/files/upload/start",
            new { fileName = "test.bin", contentType = "application/octet-stream", totalBytes = 10485760 });
        startResp.EnsureSuccessStatusCode();
        var session = await startResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(session);

        // Act - Attempt to upload block larger than MaximumTransferSizeBytes (4MB default)
        var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes("block-000000"));
        var oversizedData = new byte[5 * 1024 * 1024]; // 5MB - exceeds limit
        var content = new ByteArrayContent(oversizedData);
        var blockResp = await _client.PutAsync($"/api/files/upload/{session["blobPath"]}/block/{blockId}", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, blockResp.StatusCode);
        var error = await blockResp.Content.ReadAsStringAsync();
        Assert.Contains("Block size too large", error);
    }

    [Fact]
    public async Task ResumableUpload_InvalidContentRange_Returns400()
    {
        // Arrange - Start session
        var startResp = await _client.PostAsJsonAsync("/api/files/upload/start",
            new { fileName = "test.bin", contentType = "application/octet-stream", totalBytes = 4096 });
        startResp.EnsureSuccessStatusCode();
        var session = await startResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(session);

        // Act - Upload with mismatched Content-Range (claims 2KB but sends 1KB)
        var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes("block-000000"));
        var blockData = new byte[1024]; // 1KB
        var content = new ByteArrayContent(blockData);
        content.Headers.Add("Content-Range", "bytes 0-2047/4096"); // Claims 2KB but content is 1KB
        
        var blockResp = await _client.PutAsync($"/api/files/upload/{session["blobPath"]}/block/{blockId}", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, blockResp.StatusCode);
        var error = await blockResp.Content.ReadAsStringAsync();
        Assert.Contains("Content-Range length mismatch", error);
    }

    [Fact]
    public async Task ResumableUpload_ConcurrentBlocks_Succeeds()
    {
        // Arrange - Start session
        var startResp = await _client.PostAsJsonAsync("/api/files/upload/start",
            new { fileName = "parallel-test.bin", contentType = "application/octet-stream", totalBytes = 10485760 });
        startResp.EnsureSuccessStatusCode();
        var session = await startResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(session);

        // Act - Upload 10 blocks concurrently (1MB each)
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes($"block-{i:D6}"));
            var blockData = new byte[1024 * 1024]; // 1MB each
            new Random(i).NextBytes(blockData);
            var content = new ByteArrayContent(blockData);
            content.Headers.Add("Content-Range", $"bytes {i * 1048576}-{(i + 1) * 1048576 - 1}/10485760");
            return await _client.PutAsync($"/api/files/upload/{session["blobPath"]}/block/{blockId}", content);
        });

        var results = await Task.WhenAll(tasks);

        // Assert - All uploads should succeed
        Assert.All(results, resp =>
        {
            Assert.True(resp.IsSuccessStatusCode, $"Concurrent upload failed: {resp.StatusCode}");
        });
    }

    [Fact]
    public async Task ResumableUpload_Abort_RemovesSessionAndBlocks()
    {
        // Arrange - Start session and upload one block
        var startResp = await _client.PostAsJsonAsync("/api/files/upload/start",
            new { fileName = "abort-test.bin", contentType = "application/octet-stream", totalBytes = 1024 });
        startResp.EnsureSuccessStatusCode();
        var session = await startResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(session);
        var blobPath = session["blobPath"];

        var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes("block-000000"));
        var blockData = new byte[1024];
        var blockResp = await _client.PutAsync($"/api/files/upload/{blobPath}/block/{blockId}",
            new ByteArrayContent(blockData));
        blockResp.EnsureSuccessStatusCode();

        // Act - Abort the upload
        var abortResp = await _client.PostAsync($"/api/files/upload/{blobPath}/abort", null);
        
        // Assert
        Assert.True(abortResp.IsSuccessStatusCode);

        // Verify session was removed from repository
        var sessionRepo = _factory.Services.GetRequiredService<IUploadSessionRepository>();
        var sessionAfter = await sessionRepo.GetAsync(blobPath);
        Assert.Null(sessionAfter);
    }

    [Fact]
    public async Task ResumableUpload_SSEProgress_EmitsUpdates()
    {
        // Arrange - Start session
        var startResp = await _client.PostAsJsonAsync("/api/files/upload/start",
            new { fileName = "progress-test.bin", contentType = "application/octet-stream", totalBytes = 2048 });
        startResp.EnsureSuccessStatusCode();
        var session = await startResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(session);
        var blobPath = session["blobPath"];

        // Act - Start listening to progress (SSE) in background
        var progressUpdates = new List<string>();
        var progressTask = Task.Run(async () =>
        {
            using var progressClient = _factory.CreateClient(new WebApplicationFactoryClientOptions 
            { 
                BaseAddress = new Uri("http://localhost"),
                AllowAutoRedirect = false
            });
            var progressResp = await progressClient.GetAsync($"/api/files/upload/{blobPath}/progress",
                HttpCompletionOption.ResponseHeadersRead);
            
            if (progressResp.IsSuccessStatusCode)
            {
                using var stream = await progressResp.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);
                
                // Read up to 5 updates or until timeout
                var timeout = DateTime.UtcNow.AddSeconds(10);
                while (!reader.EndOfStream && DateTime.UtcNow < timeout && progressUpdates.Count < 5)
                {
                    var line = await reader.ReadLineAsync();
                    if (line?.StartsWith("data: ") == true)
                    {
                        progressUpdates.Add(line.Substring(6));
                    }
                }
            }
        });

        // Give SSE connection time to establish
        await Task.Delay(500);

        // Upload a block while progress is being monitored
        var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes("block-000000"));
        var blockData = new byte[1024];
        await _client.PutAsync($"/api/files/upload/{blobPath}/block/{blockId}",
            new ByteArrayContent(blockData));

        // Wait for progress updates (with timeout)
        await Task.WhenAny(progressTask, Task.Delay(15000));

        // Assert - Should have received at least one progress update
        Assert.NotEmpty(progressUpdates);
        var hasProgressData = progressUpdates.Any(u => u.Contains("\"bytes\"") || u.Contains("\"uploaded\""));
        Assert.True(hasProgressData, $"Expected progress data in updates. Got: {string.Join(", ", progressUpdates)}");
    }

    [Fact]
    public async Task ResumableUpload_CommitWithMissingBlocks_HandlesGracefully()
    {
        // Arrange - Start session and upload only one block
        var startResp = await _client.PostAsJsonAsync("/api/files/upload/start",
            new { fileName = "missing-blocks.bin", contentType = "application/octet-stream", totalBytes = 2048 });
        startResp.EnsureSuccessStatusCode();
        var session = await startResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(session);
        var blobPath = session["blobPath"];

        // Upload only block 0
        var blockId0 = Convert.ToBase64String(Encoding.UTF8.GetBytes("block-000000"));
        var blockData = new byte[1024];
        await _client.PutAsync($"/api/files/upload/{blobPath}/block/{blockId0}",
            new ByteArrayContent(blockData));

        // Act - Try to commit with a non-existent block ID
        var blockId1 = Convert.ToBase64String(Encoding.UTF8.GetBytes("block-000001"));
        var commitPayload = new
        {
            blockIds = new[] { blockId0, blockId1 }, // blockId1 was never uploaded
            fileName = "missing-blocks.bin",
            contentType = "application/octet-stream"
        };
        var commitResp = await _client.PostAsJsonAsync($"/api/files/upload/{blobPath}/commit", commitPayload);

        // Assert - Commit should succeed but only contain uploaded block
        // (StubBlobFileStorageService skips missing blocks in CommitBlocksAsync)
        Assert.True(commitResp.IsSuccessStatusCode);
        var commitData = await commitResp.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        Assert.NotNull(commitData);
        
        // Verify file was created (stub storage handles missing blocks gracefully)
        var fileId = commitData["id"].GetGuid();
        var fileResp = await _client.GetAsync($"/api/files/{fileId}");
        Assert.True(fileResp.IsSuccessStatusCode);
    }
}
