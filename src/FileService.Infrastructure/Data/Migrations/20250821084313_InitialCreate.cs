using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    OwnerUserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    BlobPath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Files", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Files_BlobPath",
                table: "Files",
                column: "BlobPath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Files_OwnerUserId_UploadedAt",
                table: "Files",
                columns: new[] { "OwnerUserId", "UploadedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Files");
        }
    }
}
