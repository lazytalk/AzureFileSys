using System.Net.Http.Headers;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using FileService.Core.Interfaces;
using System.IO;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FileService.Tests.Integration;

public class FileFlowTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public FileFlowTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Upload_List_Get_Delete_Flow_Works()
    {
        var client = _factory.CreateClient(new() { BaseAddress = new Uri("http://localhost") });
    // Authentication removed: tests run without requiring headers.

    // Use repository test file for upload
    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    var testFilePath = Path.Combine(repoRoot, "tests", "testingdatafile.dat");
    Assert.True(File.Exists(testFilePath), $"Test data file not found: {testFilePath}");
    var fileBytes = await File.ReadAllBytesAsync(testFilePath);
    // md5 of original
    string Md5Hex(byte[] b) { using var md5 = MD5.Create(); return BitConverter.ToString(md5.ComputeHash(b)).Replace("-", "").ToLowerInvariant(); }
    var originalMd5 = Md5Hex(fileBytes);

    var content = new MultipartFormDataContent();
    var sc = new ByteArrayContent(fileBytes);
    sc.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    content.Add(sc, "file", Path.GetFileName(testFilePath));

        var up = await client.PostAsync("/api/files/upload", content);
        up.EnsureSuccessStatusCode();
        var upBody = await up.Content.ReadAsStringAsync();
        using var upDoc = JsonDocument.Parse(upBody);
        var id = upDoc.RootElement.GetProperty("id").GetGuid();

        // List
    var listResp = await client.GetAsync("/api/files?all=true");
        listResp.EnsureSuccessStatusCode();
        var listBody = await listResp.Content.ReadAsStringAsync();
    Assert.Contains(Path.GetFileName(testFilePath), listBody);

        // Get details
        var getResp = await client.GetAsync($"/api/files/{id}");
        getResp.EnsureSuccessStatusCode();
        var getBody = await getResp.Content.ReadAsStringAsync();
        using var getDoc = JsonDocument.Parse(getBody);
        string? downloadUrl = null;
        foreach (var prop in getDoc.RootElement.EnumerateObject())
        {
            if (string.Equals(prop.Name, "downloadUrl", StringComparison.OrdinalIgnoreCase)) { downloadUrl = prop.Value.GetString(); break; }
        }
        Assert.False(string.IsNullOrEmpty(downloadUrl), "Expected file details to include a download URL property (downloadUrl or DownloadUrl)");

        // Retrieve bytes from the storage backend. For the stub we get blobPath from stub://{blobPath}?...
        byte[] downloaded;
        if (downloadUrl != null && downloadUrl.StartsWith("stub://"))
        {
            var blobPart = downloadUrl.Substring("stub://".Length);
            var q = blobPart.IndexOf('?');
            if (q >= 0) blobPart = blobPart.Substring(0, q);
            var storage = _factory.Services.GetRequiredService<IFileStorageService>();
            var stream = await storage.DownloadAsync(blobPart);
            Assert.NotNull(stream);
            using var ms = new MemoryStream();
            await stream!.CopyToAsync(ms);
            downloaded = ms.ToArray();
        }
        else
        {
            // Fallback: try HTTP GET
            var http = await client.GetAsync(downloadUrl!);
            http.EnsureSuccessStatusCode();
            downloaded = await http.Content.ReadAsByteArrayAsync();
        }

        var downloadedMd5 = Md5Hex(downloaded);
        Assert.Equal(originalMd5, downloadedMd5);

        // Delete
        var del = await client.DeleteAsync($"/api/files/{id}");
        Assert.True(del.IsSuccessStatusCode);

        // Confirm deleted
        var list2 = await client.GetAsync("/api/files?all=true");
        list2.EnsureSuccessStatusCode();
        var list2Body = await list2.Content.ReadAsStringAsync();
        Assert.DoesNotContain("test.txt", list2Body);
    }
}
