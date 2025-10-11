namespace FileService.Infrastructure.Storage;

public class BlobStorageOptions
{
    public string? ConnectionString { get; set; }
    public string ContainerName { get; set; } = "userfiles";
    public bool UseLocalStub { get; set; } = true; // default to stub until configured
}
