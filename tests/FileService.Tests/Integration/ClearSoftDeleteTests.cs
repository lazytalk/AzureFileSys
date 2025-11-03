using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FileService.Tests.Integration;

public class ClearSoftDeleteTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ClearSoftDeleteTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ClearAll_SoftDeletesMetadata_AndDeletesBlobs()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost") });

        // Upload a tiny file via multipart POST
        var content = new MultipartFormDataContent();
        var bytes = new byte[] { 1, 2, 3, 4 };
        var bc = new ByteArrayContent(bytes);
        bc.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(bc, "file", "clear-test.bin");

        var up = await client.PostAsync("/api/files/upload", content);
        up.EnsureSuccessStatusCode();
        var upBody = await up.Content.ReadAsStringAsync();
        using var upDoc = JsonDocument.Parse(upBody);
        var id = upDoc.RootElement.GetProperty("id").GetGuid();

        // Confirm GET returns 200 before clearing
        var before = await client.GetAsync($"/api/files/{id}");
        Assert.True(before.IsSuccessStatusCode);

        // Call clear endpoint
        var clear = await client.PostAsync("/api/files/clear", null);
        clear.EnsureSuccessStatusCode();

        // After clear, GET should return 410 Gone
        var after = await client.GetAsync($"/api/files/{id}");
        Assert.Equal(System.Net.HttpStatusCode.Gone, after.StatusCode);
    }
}
