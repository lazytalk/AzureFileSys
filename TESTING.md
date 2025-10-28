# TESTING.md

This document maps features to existing tests, highlights gaps, and provides quick instructions for running the test suite and for adding the highest-value missing tests.

## Executive Summary

**Overall Test Coverage: 60%** (4 of 7 core features fully tested)

**Critical Gaps:**
- ❌ Resumable upload flow (start → block → commit) - ZERO end-to-end tests
- ❌ Batch uploads - NOT IMPLEMENTED (server only processes first file)
- ⚠️ Concurrency enforcement - Partially tested (storage-level only)

## Current automated tests (quick map)

### ✅ tests/FileService.Tests/Integration/FileFlowTests.cs
**Coverage: Excellent** - Full CRUD cycle tested
- **Exercises:** `POST /api/files/upload` (multipart), `GET /api/files`, `GET /api/files/{id}`, server-side download path, `DELETE /api/files/{id}`
- **Verifies:** End-to-end flow using `WebApplicationFactory<Program>` and stub storage
- **Test Quality:** MD5 hash verification ensures data integrity
- **Missing:** Large file uploads (>100MB), concurrent uploads

### ✅ tests/FileService.Tests/OptimizedUploadTests.cs
**Coverage: Good** - Configuration and storage behavior tested
- **Exercises:** `BlobStorageOptions` defaults and custom values; `StubBlobFileStorageService` behaviors including concurrent UploadAsync calls, chunked upload simulation, delete-after-upload, and various file sizes (1KB to 50MB)
- **Verifies:** Storage-level behaviors and simulated chunk uploads
- **Test Quality:** Comprehensive configuration validation
- **Missing:** Azure Blob Storage integration tests, actual chunk upload performance

### ✅ tests/FileService.Tests/UploadSessionCleanupTests.cs
**Coverage: Good** - Cleanup service logic tested
- **Exercises:** `UploadSessionCleanupService` logic for querying expired sessions and aborting uploads, including retry behavior (exponential backoff with jitter) in case of transient storage failures
- **Verifies:** Expired session cleanup and repository interactions
- **Test Quality:** Simulates transient failures with configurable retry count
- **Missing:** Azure Table Storage integration, real blob cleanup verification

### ✅ tests/FileService.Tests/InMemoryRepositoryTests.cs
**Coverage: Basic** - Minimal repository operations tested
- **Exercises:** Basic add/get methods of the in-memory metadata repository
- **Missing:** Update, delete, query operations; edge cases

## Feature → Test Coverage Matrix

| Feature | Implementation | Tests | Coverage | Status | Priority |
|---------|---------------|-------|----------|--------|----------|
| **Upload (single-file multipart)** | ✅ `POST /api/files/upload` | ✅ FileFlowTests | 90% | Good | Low |
| **Download** | ✅ `GET /api/files/{id}/download` | ✅ FileFlowTests | 90% | Good | Low |
| **Delete** | ✅ `DELETE /api/files/{id}` | ✅ FileFlowTests | 90% | Good | Low |
| **List** | ✅ `GET /api/files` | ✅ FileFlowTests | 85% | Good | Low |
| **Optimized uploads** | ✅ Configurable chunking/parallelism | ⚠️ OptimizedUploadTests | 60% | Partial | Medium |
| **Resumable uploads** | ✅ Start/block/commit/abort APIs | ❌ NONE | 0% | **Critical Gap** | **High** |
| **Concurrent session enforcement** | ✅ SemaphoreSlim (MaxConcurrentUploads) | ⚠️ Storage-level only | 40% | Partial | Medium |
| **Batch uploads** | ❌ NOT IMPLEMENTED | ❌ NONE | 0% | Not Supported | Low |

### Detailed Analysis

#### ✅ Upload (single-file multipart) - 90% Coverage
**What's Tested:**
- Basic file upload with multipart form data
- Metadata creation and persistence
- MD5 hash integrity verification
- Stub storage integration

**What's Missing:**
- Large files (>100MB)
- File size limit enforcement (MaxFileSizeBytes validation)
- Content type validation
- Upload cancellation

#### ✅ Download / Download streaming endpoint - 90% Coverage
**What's Tested:**
- Server-side download streaming
- SAS URL generation (stub)
- Content-Type headers

**What's Missing:**
- Azure SAS URL generation and expiration
- Large file streaming performance
- Partial content/Range requests
- Download cancellation

#### ✅ Delete - 90% Coverage
**What's Tested:**
- Metadata deletion
- Blob deletion
- 404 handling for non-existent files

**What's Missing:**
- Concurrent delete operations
- Cascade deletion verification
- Delete retry on transient failures

#### ✅ List - 85% Coverage
**What's Tested:**
- Basic file listing
- Pagination (take parameter)

**What's Missing:**
- Filtering by owner/content type
- Sorting
- Large dataset performance

#### ⚠️ Optimized uploads (chunking, concurrency options) - 60% Coverage
**What's Tested:**
- BlobStorageOptions configuration (chunk size, concurrency, max file size)
- Concurrent storage UploadAsync calls (10 simultaneous)
- Various file sizes (1KB to 50MB)
- Stub storage chunking simulation

**What's Missing:**
- **Real Azure Blob optimized upload with actual chunking**
- Progress tracking callback verification
- Network failure and retry behavior
- Chunk upload performance benchmarks
- Memory usage validation during large uploads

#### ❌ Resumable uploads (start/block/commit/abort/progress) - 0% Coverage **CRITICAL GAP**
**What's Implemented:**
- `POST /api/files/upload/start` - Initialize session with metadata
- `PUT /api/files/upload/{blobPath}/block/{blockId}` - Upload individual blocks with Content-Range validation
- `POST /api/files/upload/{blobPath}/commit` - Finalize upload with block list
- `POST /api/files/upload/{blobPath}/abort` - Cancel incomplete upload
- `GET /api/files/upload/{blobPath}/progress` - SSE progress endpoint
- SignalR hub `/hubs/upload-progress` - Real-time progress notifications
- UploadSessionRepository - Session persistence (Azure Table Storage or in-memory)
- SemaphoreSlim enforcement per block upload
- Automatic cleanup via UploadSessionCleanupService

**What's Tested:**
- **NOTHING** - Zero end-to-end tests

**Critical Missing Tests:**
1. Session lifecycle: start → multiple blocks → commit → metadata verification
2. Content-Range header validation and enforcement
3. Block size limit enforcement (MaximumTransferSizeBytes)
4. Concurrent block uploads from same session (parallel PUT requests)
5. SemaphoreSlim concurrency limiting (MaxConcurrentUploads)
6. Progress tracking via SSE endpoint
7. SignalR progress notifications (bytes uploaded, committed status)
8. Abort endpoint cleanup (staged blocks removal, session deletion)
9. Session expiration and automatic cleanup
10. Failed block retry behavior
11. Out-of-order block uploads
12. Duplicate block ID handling
13. Missing block detection on commit
14. Large file resumable upload (>500MB)

#### ⚠️ Concurrent uploading session enforcement - 40% Coverage
**What's Tested:**
- Storage-level concurrent UploadAsync (OptimizedUploadTests)
- 10 simultaneous uploads to stub storage

**What's Missing:**
- **SemaphoreSlim enforcement in block upload endpoint**
- MaxConcurrentUploads configuration validation
- Queuing behavior when limit reached
- Timeout behavior for waiting requests
- Concurrent uploads to same blob path
- Stress testing with 50+ concurrent sessions

#### ❌ Batch uploads - 0% Coverage (NOT IMPLEMENTED)
**Current Limitation:**
```csharp
// Program.cs line ~161
var file = form.Files.FirstOrDefault(); // Only processes FIRST file
```

**What's Missing:**
- Server endpoint to accept multiple files
- Loop to process all form.Files
- Individual file result tracking (success/failure per file)
- Partial success handling
- Transaction-like semantics (all-or-nothing option)
- Batch upload tests

## Recommended High-Value Tests to Add (Prioritized)

### Priority 1: Critical - Resumable Upload Flow (MUST IMPLEMENT)

#### Test 1: Complete Resumable Upload Lifecycle
```csharp
[Fact]
public async Task ResumableUpload_StartBlockCommit_CreatesFileRecord()
{
    // 1. Start session
    var startResp = await client.PostAsJsonAsync("/api/files/upload/start", 
        new { fileName = "large-file.bin", contentType = "application/octet-stream" });
    var session = await startResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
    var blobPath = session["blobPath"];
    
    // 2. Upload 3 blocks (simulate 12MB file as 3x4MB chunks)
    var blockIds = new List<string>();
    for (int i = 0; i < 3; i++)
    {
        var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes($"block-{i:D6}"));
        blockIds.Add(blockId);
        var content = new ByteArrayContent(new byte[4 * 1024 * 1024]); // 4MB
        content.Headers.Add("Content-Range", $"bytes {i * 4194304}-{(i + 1) * 4194304 - 1}/12582912");
        var blockResp = await client.PutAsync($"/api/files/upload/{blobPath}/block/{blockId}", content);
        Assert.True(blockResp.IsSuccessStatusCode);
    }
    
    // 3. Commit blocks
    var commitResp = await client.PostAsJsonAsync($"/api/files/upload/{blobPath}/commit", 
        new { blockIds, fileName = "large-file.bin", contentType = "application/octet-stream" });
    Assert.True(commitResp.IsSuccessStatusCode);
    
    // 4. Verify metadata record created
    var commitData = await commitResp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
    var fileId = Guid.Parse(commitData["id"].ToString());
    var fileResp = await client.GetAsync($"/api/files/{fileId}");
    Assert.True(fileResp.IsSuccessStatusCode);
}
```

#### Test 2: Block Size Limit Enforcement
```csharp
[Fact]
public async Task ResumableUpload_OversizedBlock_Returns400()
{
    var startResp = await client.PostAsJsonAsync("/api/files/upload/start", 
        new { fileName = "test.bin", contentType = "application/octet-stream" });
    var session = await startResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
    
    // Attempt to upload block larger than MaximumTransferSizeBytes (4MB default)
    var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes("block-000000"));
    var content = new ByteArrayContent(new byte[5 * 1024 * 1024]); // 5MB - exceeds limit
    var blockResp = await client.PutAsync($"/api/files/upload/{session["blobPath"]}/block/{blockId}", content);
    
    Assert.Equal(HttpStatusCode.BadRequest, blockResp.StatusCode);
    var error = await blockResp.Content.ReadAsStringAsync();
    Assert.Contains("Block size too large", error);
}
```

#### Test 3: Content-Range Validation
```csharp
[Fact]
public async Task ResumableUpload_InvalidContentRange_Returns400()
{
    var startResp = await client.PostAsJsonAsync("/api/files/upload/start", 
        new { fileName = "test.bin", contentType = "application/octet-stream" });
    var session = await startResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
    
    var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes("block-000000"));
    var content = new ByteArrayContent(new byte[1024]); // 1KB
    content.Headers.Add("Content-Range", "bytes 0-2047/4096"); // Claims 2KB but sends 1KB
    
    var blockResp = await client.PutAsync($"/api/files/upload/{session["blobPath"]}/block/{blockId}", content);
    Assert.Equal(HttpStatusCode.BadRequest, blockResp.StatusCode);
}
```

#### Test 4: Concurrent Block Uploads
```csharp
[Fact]
public async Task ResumableUpload_ConcurrentBlocks_Succeeds()
{
    var startResp = await client.PostAsJsonAsync("/api/files/upload/start", 
        new { fileName = "parallel-test.bin", contentType = "application/octet-stream" });
    var session = await startResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
    
    // Upload 10 blocks concurrently
    var tasks = Enumerable.Range(0, 10).Select(async i =>
    {
        var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes($"block-{i:D6}"));
        var content = new ByteArrayContent(new byte[1024 * 1024]); // 1MB each
        return await client.PutAsync($"/api/files/upload/{session["blobPath"]}/block/{blockId}", content);
    });
    
    var results = await Task.WhenAll(tasks);
    Assert.All(results, resp => Assert.True(resp.IsSuccessStatusCode));
}
```

#### Test 5: Abort Cleanup
```csharp
[Fact]
public async Task ResumableUpload_Abort_RemovesSessionAndBlocks()
{
    var startResp = await client.PostAsJsonAsync("/api/files/upload/start", 
        new { fileName = "abort-test.bin", contentType = "application/octet-stream" });
    var session = await startResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
    var blobPath = session["blobPath"];
    
    // Upload one block
    var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes("block-000000"));
    var content = new ByteArrayContent(new byte[1024]);
    await client.PutAsync($"/api/files/upload/{blobPath}/block/{blockId}", content);
    
    // Abort
    var abortResp = await client.PostAsync($"/api/files/upload/{blobPath}/abort", null);
    Assert.True(abortResp.IsSuccessStatusCode);
    
    // Verify session removed (using internal service if accessible)
    var sessionRepo = app.Services.GetRequiredService<IUploadSessionRepository>();
    var sessionAfter = await sessionRepo.GetAsync(blobPath);
    Assert.Null(sessionAfter);
}
```

#### Test 6: SSE Progress Endpoint
```csharp
[Fact]
public async Task ResumableUpload_SSEProgress_EmitsUpdates()
{
    var startResp = await client.PostAsJsonAsync("/api/files/upload/start", 
        new { fileName = "progress-test.bin", contentType = "application/octet-stream" });
    var session = await startResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
    
    // Start listening to progress (SSE)
    var progressTask = Task.Run(async () =>
    {
        var progressResp = await client.GetAsync($"/api/files/upload/{session["blobPath"]}/progress", 
            HttpCompletionOption.ResponseHeadersRead);
        var stream = await progressResp.Content.ReadAsStreamAsync();
        var reader = new StreamReader(stream);
        var updates = new List<string>();
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (line?.StartsWith("data: ") == true)
                updates.Add(line.Substring(6));
            if (updates.Count >= 3) break; // Expect at least 3 updates
        }
        return updates;
    });
    
    // Upload blocks while progress is being monitored
    await Task.Delay(100); // Give SSE connection time to establish
    var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes("block-000000"));
    await client.PutAsync($"/api/files/upload/{session["blobPath"]}/block/{blockId}", 
        new ByteArrayContent(new byte[1024]));
    
    var progressUpdates = await progressTask;
    Assert.NotEmpty(progressUpdates);
    Assert.Contains(progressUpdates, u => u.Contains("\"bytes\":1024"));
}
```

#### Test 7: SignalR Progress Notifications
```csharp
[Fact]
public async Task ResumableUpload_SignalR_BroadcastsProgress()
{
    // Use SignalR test client (requires Microsoft.AspNetCore.SignalR.Client)
    var connection = new HubConnectionBuilder()
        .WithUrl($"http://localhost/hubs/upload-progress", options =>
        {
            options.HttpMessageHandlerFactory = _ => app.Server.CreateHandler();
        })
        .Build();
    
    var progressMessages = new List<object>();
    connection.On<object>("progress", msg => progressMessages.Add(msg));
    await connection.StartAsync();
    
    var startResp = await client.PostAsJsonAsync("/api/files/upload/start", 
        new { fileName = "signalr-test.bin", contentType = "application/octet-stream" });
    var session = await startResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
    await connection.InvokeAsync("JoinSession", session["blobPath"]);
    
    // Upload block
    var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes("block-000000"));
    await client.PutAsync($"/api/files/upload/{session["blobPath"]}/block/{blockId}", 
        new ByteArrayContent(new byte[2048]));
    
    await Task.Delay(500); // Allow SignalR propagation
    Assert.NotEmpty(progressMessages);
}
```

### Priority 2: Medium - Concurrency Enforcement

#### Test 8: MaxConcurrentUploads Enforcement
```csharp
[Fact]
public async Task ConcurrentUploads_ExceedsSemaphoreLimit_Queues()
{
    // Configure MaxConcurrentUploads to 2 for this test
    var startResp = await client.PostAsJsonAsync("/api/files/upload/start", 
        new { fileName = "concurrency-test.bin", contentType = "application/octet-stream" });
    var session = await startResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
    
    // Attempt 5 concurrent block uploads (exceeds limit of 2)
    var stopwatch = Stopwatch.StartNew();
    var tasks = Enumerable.Range(0, 5).Select(async i =>
    {
        var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes($"block-{i:D6}"));
        var startTime = stopwatch.ElapsedMilliseconds;
        var resp = await client.PutAsync($"/api/files/upload/{session["blobPath"]}/block/{blockId}", 
            new ByteArrayContent(new byte[1024]));
        var endTime = stopwatch.ElapsedMilliseconds;
        return new { Response = resp, Duration = endTime - startTime };
    });
    
    var results = await Task.WhenAll(tasks);
    
    // Some requests should be queued (longer duration)
    var sortedDurations = results.Select(r => r.Duration).OrderBy(d => d).ToList();
    Assert.True(sortedDurations[4] > sortedDurations[0] + 100); // Last request significantly delayed
}
```

### Priority 3: Low - Batch Upload Implementation

#### Test 9: Batch Upload Multiple Files
```csharp
[Fact]
public async Task BatchUpload_MultipleFiles_AllStored()
{
    var content = new MultipartFormDataContent();
    for (int i = 0; i < 5; i++)
    {
        var fileContent = new ByteArrayContent(new byte[1024]);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "files", $"file-{i}.bin");
    }
    
    var resp = await client.PostAsync("/api/files/upload/batch", content);
    Assert.True(resp.IsSuccessStatusCode);
    
    var result = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
    Assert.Equal(5, ((JsonElement)result["uploaded"]).GetArrayLength());
}
```
**Note:** Requires implementation of batch upload endpoint first.


## Implementation Recommendations

### Code Quality Improvements

1. **Add MaxFileSizeBytes Validation in Upload Endpoint**
   ```csharp
   // In POST /api/files/upload endpoint
   if (file.Length > blobOptions.Value.MaxFileSizeBytes)
       return Results.BadRequest($"File too large. Maximum: {_formatBytes(maxFileSize)}");
   ```

2. **Implement Batch Upload Support**
   ```csharp
   app.MapPost("/api/files/upload/batch", async (HttpRequest request, ...) =>
   {
       var form = await request.ReadFormAsync(ct);
       var results = new List<object>();
       foreach (var file in form.Files) // Process ALL files, not just first
       {
           try
           {
               // Upload logic here
               results.Add(new { success = true, fileName = file.FileName, id = ... });
           }
           catch (Exception ex)
           {
               results.Add(new { success = false, fileName = file.FileName, error = ex.Message });
           }
       }
       return Results.Ok(new { uploaded = results });
   });
   ```

3. **Add Session Creation in Resumable Upload Start**
   ```csharp
   app.MapPost("/api/files/upload/start", async (HttpRequest request, IUploadSessionRepository sessionRepo) =>
   {
       var meta = await request.ReadFromJsonAsync<Dictionary<string,string>>() ?? new();
       var fileName = meta.GetValueOrDefault("fileName") ?? "upload.bin";
       var contentType = meta.GetValueOrDefault("contentType") ?? "application/octet-stream";
       var totalBytes = long.Parse(meta.GetValueOrDefault("totalBytes", "0"));
       var blobPath = $"{Guid.NewGuid()}_{fileName}";
       
       // Create session in repository
       await sessionRepo.CreateAsync(blobPath, fileName, contentType, totalBytes);
       
       uploadProgress[blobPath] = 0;
       uploadCommitted[blobPath] = false;
       return Results.Ok(new { blobPath, fileName, contentType });
   });
   ```

4. **Mark Session as Committed**
   ```csharp
   app.MapPost("/api/files/upload/{blobPath}/commit", async (..., IUploadSessionRepository sessionRepo) =>
   {
       // After successful commit
       await sessionRepo.MarkCommittedAsync(blobPath, ct);
       // ... rest of logic
   });
   ```

### Architecture Improvements

1. **Extract Upload Endpoints to Controller**
   - Move resumable upload logic from `Program.cs` to `UploadController.cs`
   - Better organization and testability

2. **Add Upload Progress Service**
   - Create `IUploadProgressService` interface
   - Centralize progress tracking logic
   - Easier to mock in tests

3. **Add Validation Middleware**
   - Content-Type validation
   - File extension whitelist/blacklist
   - Virus scanning integration point

4. **Add Metrics and Monitoring**
   - Upload success/failure rates
   - Average upload times by file size
   - Concurrent upload counts
   - Session expiration rates

## Quick Commands

Run all tests (requires .NET SDK 8.0 installed):

```powershell
# From repository root
dotnet restore
dotnet test tests/FileService.Tests/FileService.Tests.csproj
```

Run with verbose output:

```powershell
dotnet test tests/FileService.Tests/FileService.Tests.csproj -v normal
```

Run specific test class:

```powershell
dotnet test --filter FullyQualifiedName~FileService.Tests.Integration.FileFlowTests
```

Run specific test method:

```powershell
dotnet test --filter FullyQualifiedName~FileService.Tests.Integration.FileFlowTests.Upload_List_Get_Delete_Flow_Works
```

Run with coverage (requires coverlet):

```powershell
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov /p:CoverletOutput=./coverage/
```

## How to Add a Resumable Integration Test (Step-by-Step Guide)

1. **Create New Test File:** `tests/FileService.Tests/Integration/ResumableUploadTests.cs`

2. **Use WebApplicationFactory Pattern:**
   ```csharp
   public class ResumableUploadTests : IClassFixture<WebApplicationFactory<Program>>
   {
       private readonly WebApplicationFactory<Program> _factory;
       private readonly HttpClient _client;

       public ResumableUploadTests(WebApplicationFactory<Program> factory)
       {
           _factory = factory;
           _client = factory.CreateClient(new() { BaseAddress = new Uri("http://localhost") });
       }
   }
   ```

3. **Start Upload Session:**
   ```csharp
   var startResp = await _client.PostAsJsonAsync("/api/files/upload/start", 
       new { fileName = "test-file.bin", contentType = "application/octet-stream", totalBytes = 3145728 });
   startResp.EnsureSuccessStatusCode();
   var session = await startResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
   var blobPath = session["blobPath"];
   ```

4. **Upload Blocks:**
   ```csharp
   var blockIds = new List<string>();
   for (int i = 0; i < 3; i++)
   {
       var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes($"block-{i:D6}"));
       blockIds.Add(blockId);
       
       var blockData = new byte[1024 * 1024]; // 1MB
       var content = new ByteArrayContent(blockData);
       content.Headers.Add("Content-Range", $"bytes {i * 1048576}-{(i + 1) * 1048576 - 1}/3145728");
       
       var blockResp = await _client.PutAsync($"/api/files/upload/{blobPath}/block/{blockId}", content);
       blockResp.EnsureSuccessStatusCode();
   }
   ```

5. **Commit Blocks:**
   ```csharp
   var commitResp = await _client.PostAsJsonAsync($"/api/files/upload/{blobPath}/commit", 
       new { blockIds, fileName = "test-file.bin", contentType = "application/octet-stream" });
   commitResp.EnsureSuccessStatusCode();
   var result = await commitResp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
   var fileId = Guid.Parse(result["id"].ToString());
   ```

6. **Verify File Created:**
   ```csharp
   var fileResp = await _client.GetAsync($"/api/files/{fileId}");
   fileResp.EnsureSuccessStatusCode();
   var fileData = await fileResp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
   Assert.Equal("test-file.bin", fileData["fileName"].ToString());
   Assert.Equal(3145728, ((JsonElement)fileData["sizeBytes"]).GetInt64());
   ```

7. **Test SignalR (Advanced):**
   ```csharp
   // Requires: Microsoft.AspNetCore.SignalR.Client NuGet package
   var connection = new HubConnectionBuilder()
       .WithUrl("http://localhost/hubs/upload-progress", options =>
       {
           options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
       })
       .Build();
   
   var progressReceived = new TaskCompletionSource<object>();
   connection.On<object>("progress", msg => progressReceived.TrySetResult(msg));
   
   await connection.StartAsync();
   await connection.InvokeAsync("JoinSession", blobPath);
   
   // Upload block...
   
   var progressMsg = await progressReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
   Assert.NotNull(progressMsg);
   ```

## Notes & Assumptions

- Tests run against in-memory/stub storage by default (controlled by `BlobStorage:UseLocalStub` in test configuration). This keeps tests hermetic and fast.
- If you require tests against Azure Blob Storage or Azure Table Storage, add separate integration tests marked with `[Trait("Category", "AzureIntegration")]` and run only in CI with appropriate secrets.
- SignalR tests require the `Microsoft.AspNetCore.SignalR.Client` package.
- For SSE tests, use `HttpCompletionOption.ResponseHeadersRead` to stream response.

## Test Performance Guidelines

- Unit tests should complete in <100ms each
- Integration tests (in-memory) should complete in <1s each
- Azure integration tests may take 3-5s each
- Full test suite should complete in <30s (without Azure integration)

## Known Test Limitations

1. **Stub Storage Limitations:**
   - Doesn't test actual Azure Blob Storage behavior
   - No network latency simulation
   - No actual SAS URL generation testing
   - Block blob behavior is simulated, not real

2. **In-Memory Repository Limitations:**
   - No database concurrency testing
   - No transaction behavior testing
   - Data lost between test runs (by design)

3. **Missing Performance Tests:**
   - Large file uploads (>1GB)
   - Sustained concurrent load
   - Memory leak detection
   - Network failure recovery

---

**Last Updated:** October 28, 2025  
**Test Coverage Goal:** 90% by end of Q4 2025  
**Critical Gap:** Resumable upload end-to-end tests (highest priority)
