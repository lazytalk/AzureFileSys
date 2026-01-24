using FileService.Core.Interfaces;
using FileService.Infrastructure.Storage;
using FileService.Infrastructure.Data;
using FileService.Api.Models;
using FileService.Api.Services;
using FileService.Api.Middleware;
using FileService.Api.Endpoints;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Mvc;

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

// CORS: allow origins via configuration
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalTools", policy =>
    {
        if (builder.Environment.IsDevelopment() || builder.Environment.IsStaging())
        {
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            if (corsOrigins.Length > 0)
            {
                policy.WithOrigins(corsOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            }
            else
            {
                policy.SetIsOriginAllowed(_ => false);
            }
        }
    });
});

// Simple PowerSchool auth stub middleware registration
builder.Services.AddScoped<PowerSchoolUserContext>();

// Background cleanup service for exported zip files
builder.Services.AddHostedService<ExportCleanupService>();

// OpenID Relying Party configuration for PowerSchool authentication
// Enable by default unless explicitly disabled
var enableOpenId = builder.Configuration.GetValue<bool>("OpenId:Enabled", true);
FileService.Api.Services.OpenIdRelyingPartyService? openIdService = null;
if (enableOpenId)
{
    var ipHostname = builder.Configuration.GetValue<string>("OpenId:Hostname") ?? 
                     builder.Configuration.GetValue<string>("OpenId:IpHostname") ?? 
                     "localhost";
    var port = builder.Configuration.GetValue<int>("OpenId:Port", 443);

    if (!string.IsNullOrEmpty(ipHostname))
    {
        openIdService = new FileService.Api.Services.OpenIdRelyingPartyService(ipHostname, port);
        Console.WriteLine($"[STARTUP] OpenID Relying Party enabled at https://{ipHostname}:{port}");
    }
    else
    {
        Console.WriteLine("[STARTUP WARNING] OpenID enabled but Hostname is not configured");
    }
}
else
{
    Console.WriteLine("[STARTUP] OpenID Relying Party disabled");
}

// Simple in-memory job tracker for zip generation
var zipJobs = new System.Collections.Concurrent.ConcurrentDictionary<Guid, ZipJobStatus>();

var app = builder.Build();

var isDevMode = builder.Environment.IsDevelopment();

app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();

app.UseCors("LocalTools");

// Configure default files (serves index.html when accessing root /)
app.UseDefaultFiles(new DefaultFilesOptions
{
    DefaultFileNames = new List<string> { "index.html" }
});
app.UseStaticFiles();

// OpenID Relying Party endpoints (MUST be before terminal middleware)
if (openIdService != null)
{
    FileService.Api.Services.OpenIdRelyingPartyExtensions.MapOpenIdAuthentication(app, openIdService);
}

// PowerSchool authentication middleware
app.UsePowerSchoolAuthentication();

// ===== ENDPOINT ROUTING GATEWAY =====
// All API endpoints are registered here through extension methods.
// Each endpoint group corresponds to a specific API domain.

// PowerSchool authentication endpoints (dev-only)
app.MapPowerSchoolAuthEndpoints();

// Health check endpoint
app.MapHealthCheckEndpoints();

// File operations endpoints (CRUD)
app.MapFileOperationEndpoints();

// Zip batch download endpoints (async operations)
app.MapZipDownloadEndpoints();

app.Run();
