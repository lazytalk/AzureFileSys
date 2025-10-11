using FileService.Core.Entities;
using FileService.Infrastructure.Storage;
using Xunit;

namespace FileService.Tests;

public class InMemoryRepositoryTests
{
    [Fact]
    public async Task Add_And_Get_Works()
    {
        var repo = new InMemoryFileMetadataRepository();
        var rec = new FileRecord { FileName = "test.txt", OwnerUserId = "user1", ContentType = "text/plain", SizeBytes = 10, BlobPath = "user1/test.txt" };
        await repo.AddAsync(rec);
        var fetched = await repo.GetAsync(rec.Id);
        Assert.NotNull(fetched);
        Assert.Equal(rec.FileName, fetched!.FileName);
    }
}
