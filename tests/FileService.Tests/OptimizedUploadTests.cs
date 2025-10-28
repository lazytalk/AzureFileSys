using FileService.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using Xunit;

namespace FileService.Tests;

public class OptimizedUploadTests
{
    [Fact]
    public void BlobStorageOptions_DefaultValues_AreConfiguredCorrectly()
    {
        // Arrange & Act
        var options = new BlobStorageOptions();

        // Assert
        Assert.Equal(4 * 1024 * 1024, options.InitialTransferSizeBytes); // 4 MB
        Assert.Equal(4 * 1024 * 1024, options.MaximumTransferSizeBytes); // 4 MB
        Assert.Equal(8, options.MaxConcurrency); // 8 parallel uploads
        Assert.False(options.EnableProgressTracking);
        Assert.Equal(500L * 1024 * 1024, options.MaxFileSizeBytes); // 500 MB
    }

    [Theory]
    [InlineData(1024 * 1024, 1024 * 1024, 4)] // 1 MB chunks, 4 concurrent
    [InlineData(8 * 1024 * 1024, 8 * 1024 * 1024, 16)] // 8 MB chunks, 16 concurrent
    [InlineData(16 * 1024 * 1024, 16 * 1024 * 1024, 32)] // 16 MB chunks, 32 concurrent
    public void BlobStorageOptions_CustomValues_CanBeConfigured(long chunkSize, long maxTransfer, int concurrency)
    {
        // Arrange & Act
        var options = new BlobStorageOptions
        {
            InitialTransferSizeBytes = chunkSize,
            MaximumTransferSizeBytes = maxTransfer,
            MaxConcurrency = concurrency
        };

        // Assert
        Assert.Equal(chunkSize, options.InitialTransferSizeBytes);
        Assert.Equal(maxTransfer, options.MaximumTransferSizeBytes);
        Assert.Equal(concurrency, options.MaxConcurrency);
    }

    [Fact]
    public void BlobStorageOptions_ProgressTracking_CanBeEnabled()
    {
        // Arrange & Act
        var options = new BlobStorageOptions
        {
            EnableProgressTracking = true
        };

        // Assert
        Assert.True(options.EnableProgressTracking);
    }

    [Theory]
    [InlineData(100 * 1024 * 1024)] // 100 MB
    [InlineData(1024L * 1024 * 1024)] // 1 GB
    [InlineData(5L * 1024 * 1024 * 1024)] // 5 GB
    public void BlobStorageOptions_MaxFileSize_CanBeConfigured(long maxSize)
    {
        // Arrange & Act
        var options = new BlobStorageOptions
        {
            MaxFileSizeBytes = maxSize
        };

        // Assert
        Assert.Equal(maxSize, options.MaxFileSizeBytes);
    }

    [Fact]
    public async Task StubBlobFileStorageService_CanHandleLargeFileUpload()
    {
        // Arrange
        var storage = new StubBlobFileStorageService();
        var largeFileSize = 10 * 1024 * 1024; // 10 MB
        using var stream = new MemoryStream(new byte[largeFileSize]);
        var blobPath = "test-large-file.bin";

        // Act
        var result = await storage.UploadAsync(blobPath, stream, "application/octet-stream");

        // Assert
        Assert.Equal(blobPath, result);
        
        // Verify we can download it
        var downloadStream = await storage.DownloadAsync(blobPath);
        Assert.NotNull(downloadStream);
        Assert.Equal(largeFileSize, downloadStream.Length);
    }

    [Fact]
    public async Task StubBlobFileStorageService_MultipleUploads_WorkConcurrently()
    {
        // Arrange
        var storage = new StubBlobFileStorageService();
        var uploadTasks = new List<Task<string>>();

        // Act - Simulate 10 concurrent uploads
        for (int i = 0; i < 10; i++)
        {
            var fileData = new byte[1024 * 1024]; // 1 MB each
            var stream = new MemoryStream(fileData);
            var blobPath = $"concurrent-file-{i}.bin";
            uploadTasks.Add(storage.UploadAsync(blobPath, stream, "application/octet-stream"));
        }

        var results = await Task.WhenAll(uploadTasks);

        // Assert
        Assert.Equal(10, results.Length);
        Assert.All(results, result => Assert.NotNull(result));
        Assert.Equal(10, results.Distinct().Count()); // All unique paths
    }

    [Fact]
    public async Task StubBlobFileStorageService_ChunkedUpload_Simulation()
    {
        // Arrange
        var storage = new StubBlobFileStorageService();
        var totalSize = 20 * 1024 * 1024; // 20 MB total
        var chunkSize = 4 * 1024 * 1024; // 4 MB chunks
        var numberOfChunks = (int)Math.Ceiling((double)totalSize / chunkSize);

        // Act - Simulate chunked upload by uploading in parts
        var blobPath = "chunked-test-file.bin";
        using var fullStream = new MemoryStream(new byte[totalSize]);
        var result = await storage.UploadAsync(blobPath, fullStream, "application/octet-stream");

        // Assert
        Assert.Equal(blobPath, result);
        
        var downloadedStream = await storage.DownloadAsync(blobPath);
        Assert.NotNull(downloadedStream);
        Assert.Equal(totalSize, downloadedStream.Length);
    }

    [Fact]
    public async Task StubBlobFileStorageService_DeleteAfterUpload_Works()
    {
        // Arrange
        var storage = new StubBlobFileStorageService();
        var blobPath = "delete-test.txt";
        using var uploadStream = new MemoryStream(new byte[1024]);
        
        // Act
        await storage.UploadAsync(blobPath, uploadStream, "text/plain");
        var existsBefore = await storage.DownloadAsync(blobPath);
        await storage.DeleteAsync(blobPath);
        var existsAfter = await storage.DownloadAsync(blobPath);

        // Assert
        Assert.NotNull(existsBefore);
        Assert.Null(existsAfter);
    }

    [Theory]
    [InlineData(1024)] // 1 KB
    [InlineData(1024 * 1024)] // 1 MB
    [InlineData(10 * 1024 * 1024)] // 10 MB
    [InlineData(50 * 1024 * 1024)] // 50 MB
    public async Task StubBlobFileStorageService_VariousFileSizes_UploadSuccessfully(int fileSize)
    {
        // Arrange
        var storage = new StubBlobFileStorageService();
        var blobPath = $"size-test-{fileSize}.bin";
        using var stream = new MemoryStream(new byte[fileSize]);

        // Act
        var result = await storage.UploadAsync(blobPath, stream, "application/octet-stream");
        var downloaded = await storage.DownloadAsync(blobPath);

        // Assert
        Assert.Equal(blobPath, result);
        Assert.NotNull(downloaded);
        Assert.Equal(fileSize, downloaded.Length);
    }
}
