using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FileService.Tests.Integration;

public class CorsAndSignalRTests
{
    [Fact]
    public async Task CorsPreflight_ReturnsCorsHeaders_WhenEnabled()
    {
        // Create factory enabling CORS via in-memory config
        var factory = new TestWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((ctx, conf) =>
            {
                conf.AddInMemoryCollection(new[] {
                    new KeyValuePair<string, string?>("Features:EnableCors", "true"),
                    new KeyValuePair<string, string?>("Cors:AllowedOrigins:0", "http://localhost")
                });
            });
        });

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost") });

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/files");
        request.Headers.Add("Origin", "http://localhost");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var resp = await client.SendAsync(request);
        Assert.True(resp.IsSuccessStatusCode, "Expected preflight to succeed");
        Assert.True(resp.Headers.Contains("Access-Control-Allow-Origin") || resp.Content.Headers.Contains("Access-Control-Allow-Origin"), "Missing CORS allow origin header");
    }

    [Fact]
    public async Task SignalR_Hub_ReceivesProgress_WhenUploadBlockTriggered()
    {
        var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost") });

        // Start a resumable session
        var startResp = await client.PostAsJsonAsync("/api/files/upload/start", new { fileName = "signalr.bin", contentType = "application/octet-stream", totalBytes = 1024 });
        startResp.EnsureSuccessStatusCode();
        var session = await startResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var blobPath = session!["blobPath"];

        // Create HubConnection using test server handler
        var hubUrl = new Uri(client.BaseAddress!, "/hubs/upload-progress").ToString();
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        var received = new List<string>();
        connection.On<object>("progress", payload =>
        {
            received.Add(System.Text.Json.JsonSerializer.Serialize(payload));
        });

    await connection.StartAsync();

    // Join the group so the client receives messages targeted to the blobPath
    await connection.InvokeAsync("JoinSession", blobPath);
        // Upload a single block to trigger progress message
        var blockId = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("block-000000"));
        var blockData = new byte[512];
        var content = new ByteArrayContent(blockData);
        var blockResp = await client.PutAsync($"/api/files/upload/{blobPath}/block/{blockId}", content);
        blockResp.EnsureSuccessStatusCode();

    // Wait briefly for message delivery
    await Task.Delay(1500);

        await connection.StopAsync();
        Assert.NotEmpty(received);
    }
}
