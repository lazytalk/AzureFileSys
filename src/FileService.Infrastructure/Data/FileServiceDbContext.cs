using FileService.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace FileService.Infrastructure.Data;

public class FileServiceDbContext : DbContext
{
    public FileServiceDbContext(DbContextOptions<FileServiceDbContext> options) : base(options) {}

    public DbSet<FileRecord> Files => Set<FileRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FileRecord>(b =>
        {
            b.ToTable("Files");
            b.HasKey(f => f.Id);
            b.Property(f => f.FileName).HasMaxLength(512);
            b.Property(f => f.OwnerUserId).HasMaxLength(128).IsRequired();
            b.Property(f => f.BlobPath).HasMaxLength(1024).IsRequired();
            b.HasIndex(f => new { f.OwnerUserId, f.UploadedAt });
            b.HasIndex(f => f.BlobPath).IsUnique();
        });
    }
}
