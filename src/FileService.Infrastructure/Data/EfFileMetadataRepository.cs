using FileService.Core.Entities;
using FileService.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FileService.Infrastructure.Data;

public class EfFileMetadataRepository : IFileMetadataRepository
{
    private readonly FileServiceDbContext _ctx;
    public EfFileMetadataRepository(FileServiceDbContext ctx) => _ctx = ctx;

    public async Task AddAsync(FileRecord record, CancellationToken ct = default)
    {
        _ctx.Files.Add(record);
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _ctx.Files.FindAsync([id], ct);
        if (entity != null)
        {
            _ctx.Files.Remove(entity);
            await _ctx.SaveChangesAsync(ct);
        }
    }

    public Task<FileRecord?> GetAsync(Guid id, CancellationToken ct = default)
        => _ctx.Files.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<IReadOnlyList<FileRecord>> ListAllAsync(int take = 200, int skip = 0, CancellationToken ct = default)
    {
        return await _ctx.Files.AsNoTracking()
            .OrderByDescending(f => f.UploadedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FileRecord>> ListByOwnerAsync(string ownerUserId, CancellationToken ct = default)
    {
        return await _ctx.Files.AsNoTracking()
            .Where(f => f.OwnerUserId == ownerUserId)
            .OrderByDescending(f => f.UploadedAt)
            .ToListAsync(ct);
    }
}
