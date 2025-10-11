using FileService.Core.Entities;
using FileService.Core.Interfaces;

namespace FileService.Infrastructure.Storage;

// Temporary in-memory repository until a real database (Azure SQL / Cosmos DB) is wired.
public class InMemoryFileMetadataRepository : IFileMetadataRepository
{
    private readonly Dictionary<Guid, FileRecord> _store = new();

    public Task AddAsync(FileRecord record, CancellationToken ct = default)
    {
        _store[record.Id] = record;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _store.Remove(id);
        return Task.CompletedTask;
    }

    public Task<FileRecord?> GetAsync(Guid id, CancellationToken ct = default)
    {
        _store.TryGetValue(id, out var rec);
        return Task.FromResult(rec);
    }

    public Task<IReadOnlyList<FileRecord>> ListAllAsync(int take = 200, int skip = 0, CancellationToken ct = default)
    {
        var list = _store.Values
            .OrderByDescending(f => f.UploadedAt)
            .Skip(skip)
            .Take(take)
            .ToList();
        return Task.FromResult((IReadOnlyList<FileRecord>)list);
    }

    public Task<IReadOnlyList<FileRecord>> ListByOwnerAsync(string ownerUserId, CancellationToken ct = default)
    {
        var list = _store.Values
            .Where(f => f.OwnerUserId.Equals(ownerUserId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.UploadedAt)
            .ToList();
        return Task.FromResult((IReadOnlyList<FileRecord>)list);
    }
}
