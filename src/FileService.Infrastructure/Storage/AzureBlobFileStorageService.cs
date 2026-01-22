using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using FileService.Core.Interfaces;
using Microsoft.Extensions.Options;
using System.Collections.Generic;

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
        // OpenReadAsync is better for large files than loading into memory
        return await blob.OpenReadAsync(new BlobOpenReadOptions(false), ct);
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

    public Task<string> GetWriteSasUrlAsync(string blobPath, TimeSpan ttl, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(blobPath);
        if (!blob.CanGenerateSasUri)
        {
            // For connection strings that don't support SAS (e.g. AD auth without delegation), 
            // this might fail or return a raw URI which won't work for write without auth.
            // But usually with shared key credential it works.
            return Task.FromResult(blob.Uri.ToString());
        }
        var sasBuilder = new BlobSasBuilder(BlobSasPermissions.Create | BlobSasPermissions.Write, DateTimeOffset.UtcNow.Add(ttl))
        {
            BlobContainerName = blob.BlobContainerName,
            BlobName = blob.Name,
            Resource = "b"
        };
        var sasUri = blob.GenerateSasUri(sasBuilder);
        return Task.FromResult(sasUri.ToString());
    }

    public async Task<long?> GetBlobSizeAsync(string blobPath, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(blobPath);
        if (!await blob.ExistsAsync(ct)) return null;
        var props = await blob.GetPropertiesAsync(cancellationToken: ct);
        return props.Value.ContentLength;
    }

    public async Task<Stream> OpenWriteAsync(string blobPath, string contentType, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(blobPath);
        // OpenWriteAsync returns a stream that writes to the blob.
        // overwrite: true is default behavior for OpenWriteAsync if not specified, 
        // but we can be explicit if needed.
        return await blob.OpenWriteAsync(overwrite: true, new BlobOpenWriteOptions 
        { 
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType } 
        }, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<BlobItemInfo>> ListAsync(string prefix, CancellationToken ct = default)
    {
        var list = new List<BlobItemInfo>();
        await foreach (var item in _container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, ct))
        {
            list.Add(new BlobItemInfo(item.Name, item.Properties.LastModified));
        }
        return list;
    }
}
