using FileService.Core.Entities;

namespace FileService.Core.Interfaces;

public interface IFileMetadataRepository
{
    Task<FileRecord?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<FileRecord>> ListByOwnerAsync(string ownerUserId, CancellationToken ct = default);
    // Returns non-deleted files only
    Task<IReadOnlyList<FileRecord>> ListAllAsync(int take = 200, int skip = 0, CancellationToken ct = default);
    Task AddAsync(FileRecord record, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    // Marks metadata as deleted without removing the record
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
}
