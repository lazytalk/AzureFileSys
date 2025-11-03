namespace FileService.Infrastructure.Storage;

public class BlobStorageOptions
{
    public string? ConnectionString { get; set; }
    public string ContainerName { get; set; } = "userfiles";
    public bool UseLocalStub { get; set; } = true; // default to stub until configured
    
    // Upload optimization settings
    /// <summary>
    /// Initial transfer size for the first chunk (bytes). Default: 4 MB
    /// </summary>
    public long? InitialTransferSizeBytes { get; set; } = 4 * 1024 * 1024; // 4 MB
    
    /// <summary>
    /// Maximum transfer size per block (bytes). Default: 4 MB. Max allowed by Azure: 4000 MB
    /// </summary>
    public long? MaximumTransferSizeBytes { get; set; } = 4 * 1024 * 1024; // 4 MB
    
    /// <summary>
    /// Maximum number of parallel upload workers. Default: 8
    /// </summary>
    public int? MaxConcurrency { get; set; } = 8;
    
    /// <summary>
    /// Enable progress tracking for uploads (logs progress to console). Default: false
    /// </summary>
    public bool EnableProgressTracking { get; set; } = false;
    
    /// <summary>
    /// Maximum file size allowed for upload (bytes). Default: 500 MB. Azure supports up to ~190 TB
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 500L * 1024 * 1024; // 500 MB
}
