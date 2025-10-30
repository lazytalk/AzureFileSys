using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;

namespace FileService.Tests;

/// <summary>
/// Custom WebApplicationFactory that disables Swagger by default for test runs
/// and exposes a helper to add configuration overrides per test.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Disable Swagger/UI during tests by default to avoid middleware races
        builder.ConfigureAppConfiguration((context, conf) =>
        {
            conf.AddInMemoryCollection(new[] {
                new KeyValuePair<string, string?>("Features:EnableSwagger", "false")
            });
        });
        base.ConfigureWebHost(builder);
    }

    /// <summary>
    /// Create a factory with additional in-memory configuration overrides.
    /// </summary>
    public TestWebApplicationFactory WithConfigOverrides(IEnumerable<KeyValuePair<string, string?>> overrides)
    {
        var w = WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((context, conf) =>
            {
                conf.AddInMemoryCollection(overrides);
            });
        });
        return (TestWebApplicationFactory)w;
    }
}
