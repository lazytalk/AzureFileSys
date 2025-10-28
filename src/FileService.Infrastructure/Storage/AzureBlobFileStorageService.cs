using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using FileService.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace FileService.Infrastructure.Storage;

public class AzureBlobFileStorageService : IFileStorageService
{
    private readonly BlobContainerClient _container;
    private readonly BlobStorageOptions _options;

    public AzureBlobFileStorageService(IOptions<BlobStorageOptions> options)
    {
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            throw new InvalidOperationException("Blob storage connection string not configured.");
        _container = new BlobContainerClient(_options.ConnectionString, _options.ContainerName);
        _container.CreateIfNotExists(PublicAccessType.None);
    }

    public async Task<string> UploadAsync(string blobPath, Stream content, string contentType, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(blobPath);
        
        // Configure optimized upload with chunking and parallelism
        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            TransferOptions = new Azure.Storage.StorageTransferOptions
            {
                // Configure chunk size (default 4MB, max 4000MB per block)
                // Azure Blob supports up to 50,000 blocks of 4000MB each = 190TB max
                InitialTransferSize = _options.InitialTransferSizeBytes ?? 4 * 1024 * 1024, // 4 MB initial
                MaximumTransferSize = _options.MaximumTransferSizeBytes ?? 4 * 1024 * 1024, // 4 MB per block
                
                // Enable parallel uploads for better performance
                MaximumConcurrency = _options.MaxConcurrency ?? 8 // 8 parallel uploads
            },
            
            // Optional: Add progress handler if needed
            ProgressHandler = _options.EnableProgressTracking ? new Progress<long>(bytesTransferred =>
            {
                // This can be logged or reported via SignalR/SSE
                Console.WriteLine($"[UPLOAD PROGRESS] {blobPath}: {bytesTransferred} bytes transferred");
            }) : null
        };

        await blob.UploadAsync(content, uploadOptions, cancellationToken: ct);
        return blobPath;
    }

    public async Task UploadBlockAsync(string blobPath, string base64BlockId, Stream content, CancellationToken ct = default)
    {
        var blockBlob = new Azure.Storage.Blobs.Specialized.BlockBlobClient(_options.ConnectionString, _container.Name, blobPath);
        // StageBlockAsync expects Base64-encoded block ID and the content stream
        await blockBlob.StageBlockAsync(base64BlockId, content, cancellationToken: ct);
    }

    public async Task CommitBlocksAsync(string blobPath, IEnumerable<string> base64BlockIds, string contentType, CancellationToken ct = default)
    {
        var blockBlob = new Azure.Storage.Blobs.Specialized.BlockBlobClient(_options.ConnectionString, _container.Name, blobPath);
        await blockBlob.CommitBlockListAsync(base64BlockIds, new BlobHttpHeaders { ContentType = contentType }, cancellationToken: ct);
    }

    public async Task AbortUploadAsync(string blobPath, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(blobPath);
        await blob.DeleteIfExistsAsync(cancellationToken: ct);
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
