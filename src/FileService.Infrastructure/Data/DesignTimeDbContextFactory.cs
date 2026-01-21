using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FileService.Infrastructure.Data
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FileServiceDbContext>
    {
        public FileServiceDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<FileServiceDbContext>();

            // Prefer SQL Server to match staging/production provider
            var conn = Environment.GetEnvironmentVariable("Sql__ConnectionString")
                       ?? "Server=(localdb)\\mssqllocaldb;Database=FileServiceDesignTime;Trusted_Connection=True;MultipleActiveResultSets=true";
            optionsBuilder.UseSqlServer(conn);

            return new FileServiceDbContext(optionsBuilder.Options);
        }
    }
}