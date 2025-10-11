using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using FileService.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace FileService.Infrastructure.Storage;

public class AzureBlobFileStorageService : IFileStorageService
{
    private readonly BlobContainerClient _container;

    public AzureBlobFileStorageService(IOptions<BlobStorageOptions> options)
    {
        var opt = options.Value;
        if (string.IsNullOrWhiteSpace(opt.ConnectionString))
            throw new InvalidOperationException("Blob storage connection string not configured.");
        _container = new BlobContainerClient(opt.ConnectionString, opt.ContainerName);
        _container.CreateIfNotExists(PublicAccessType.None);
    }

    public async Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(blobPath);
        await blob.UploadAsync(content, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: ct);
        return blobPath;
    }

    public async Task<Stream?> DownloadAsync(string blobPath, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(blobPath);
        if (!await blob.ExistsAsync(ct)) return null;
        var ms = new MemoryStream();
        await blob.DownloadToAsync(ms, ct);
        ms.Position = 0;
        return ms;
    }

    public async Task DeleteAsync(string blobPath, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(blobPath);
        await blob.DeleteIfExistsAsync(cancellationToken: ct);
    }

    public Task<string> GetReadSasUrlAsync(string blobPath, TimeSpan ttl, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(blobPath);
        if (!blob.CanGenerateSasUri)
        {
            return Task.FromResult(blob.Uri.ToString());
        }
        var sasBuilder = new BlobSasBuilder(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(ttl))
        {
            BlobContainerName = blob.BlobContainerName,
            BlobName = blob.Name,
            Resource = "b"
        };
        var sasUri = blob.GenerateSasUri(sasBuilder);
        return Task.FromResult(sasUri.ToString());
    }
}
