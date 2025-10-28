# CODEBASE.md — File map and purpose

This file is a concise, developer-focused map of the repository. It lists the important files and folders and a one-line purpose for each. Use this as the primary entry point when exploring the codebase.

Root
----
- `FileService.sln` — Visual Studio solution that groups all projects.
- `README.md` — High-level project overview and basic run/build examples.
- `DEV-ARCHITECTURE.md` — Architecture and development guidance (longer form than README).
- `DEPLOY.md` — Deployment notes and important production considerations.
- `CODEBASE.md` — (This file) per-file map for quick reference.
- `scripts/` — PowerShell scripts used for development, migration, deploy, and smoke tests.

src/
----
All source code projects live under `src/`.

- `src/FileService.Api/` — ASP.NET Core web application (Minimal APIs + Razor Pages).
  - `Program.cs` — Application bootstrap: dependency injection, endpoint mappings (upload/list/get/download/delete), environment wiring, and middleware. Key place to change DI or configuration.
  - Resumable upload endpoints are implemented in `Program.cs` (see `/api/files/upload/start`, `/api/files/upload/{blobPath}/block/{blockId}`, `/api/files/upload/{blobPath}/commit`, `/api/files/upload/{blobPath}/abort`, and `/api/files/upload/{blobPath}/progress`). These endpoints rely on `UploadSessionRepository` and `IFileStorageService` implementations.
  - Note: the codebase currently treats `POST /api/files/upload` as single-file uploads (the server uses the first file in the form). There is no server-side batch-upload endpoint yet; the admin UI also uploads only the first file selected.
  - `appsettings.json` / `appsettings.Development.json` / `appsettings.Staging.json` / `appsettings.Production.json` — Environment-specific configuration; contains `BlobStorage`, `Persistence`, `Features`, etc.
  - `Pages/Admin.cshtml` — Client-side (HTML + JS) admin UI for quick manual testing of upload/list/download/delete.
  - `Pages/IntegrationTest.cshtml` (+ `.cshtml.cs`) — Razor Page used for quick integration flows and server-side test harness.
  - `Controllers/` — (if present) controller classes; minimal APIs live in `Program.cs` for now.
  - `Properties/launchSettings.json` — Local launch configuration.

- `src/FileService.Core/` — Domain layer: entities, DTOs, interfaces.
  - `Entities/FileRecord.cs` — The metadata model persisted in database (Id, FileName, BlobPath, ContentType, SizeBytes, OwnerUserId, UploadedAt).
  - `Interfaces/IFileMetadataRepository.cs` — Abstraction for metadata persistence (Add/Get/List/Delete).
  - `Interfaces/IFileStorageService.cs` — Abstraction for storing blobs (Upload/Download/Delete/GetReadSasUrlAsync).
  - `Models/FileListItemDto.cs` — Lightweight DTO returned by list endpoints.

- `src/FileService.Infrastructure/` — Implementations for storage and persistence.
  - `Data/FileServiceDbContext.cs` — EF Core DbContext and DbSet<FileRecord> mapping.
  - `Data/EfFileMetadataRepository.cs` — EF-based implementation of `IFileMetadataRepository`.
  - `Storage/AzureBlobFileStorageService.cs` — Production Azure Blob Storage implementation using Azure.Storage.Blobs SDK (requires `BlobStorage:ConnectionString`).
  - `Storage/StubBlobFileStorageService.cs` — In-memory stub used in development; returns `stub://` pseudo-URLs.
  - `Storage/BlobStorageOptions.cs` — POCO for `BlobStorage` configuration (ConnectionString, ContainerName, UseLocalStub).
  - `InMemoryFileMetadataRepository.cs` — Lightweight metadata store for dev/testing.

tests/
-----
- `tests/FileService.Tests/` — xUnit test project with unit and integration tests.
  - `Integration/FileFlowTests.cs` — In-process integration tests using `WebApplicationFactory<Program>` that exercise upload → list → download → delete flows.

scripts/
-------
- `dev-run.ps1` — Start local dev server with environment defaults (uses in-memory/stub behavior by default).
- `migrate-dev.ps1` — Apply EF migrations in local dev (if using SQLite or configured DB).
- `deploy-staging.ps1` — Script to provision staging resources in Azure (Resource Group, Storage, SQL, Key Vault, App Service) and set app settings referencing Key Vault.
- `deploy-production.ps1` — Similar to staging but targeted for production tiers.
- `smoke-test.ps1` — End-to-end smoke test helper that exercises the running service.
- `open-dev-db.ps1` — Open the local SQLite DB for inspection.

.vscode/
-------
- `tasks.json` / `launch.json` — VS Code tasks and launch configs to run/start the API for development.

Important configuration keys (where the app reads settings)
-------------------------------------------------------
- `BlobStorage:UseLocalStub` (bool) — true uses `StubBlobFileStorageService`; false uses Azure Blob Storage.
- `BlobStorage:ConnectionString` (string) — Azure Storage connection string, typically sourced from Key Vault in staging/prod.
- `BlobStorage:ContainerName` (string) — Blob container name used for uploads.
- `Persistence:UseEf` (bool) — whether to use EF-based repository.
- `Persistence:UseSqlServer` (bool) — when true, code will try to use SQL Server connection string (`Sql__ConnectionString`) for DbContext.
- `Persistence:SqlitePath` (string) — local SQLite DB path used as fallback.
- `Persistence:AutoMigrate` (bool) — when true the app will run `Database.Migrate()` at startup.
- `Sql__ConnectionString` — app setting name used in deploy scripts to reference the SQL connection string secret in Key Vault.

How the environment wiring works (quick)
-------------------------------------
- Development: by default `EnvironmentMode=Development` and `builder.Environment.IsDevelopment()` triggers the in-memory metadata repo and stub blob storage so you can iterate without external services.
- Staging/Production: configuration should set `BlobStorage:UseLocalStub=false` and provide `BlobStorage:ConnectionString` via Key Vault; set `Persistence:UseEf=true` and `Persistence:UseSqlServer=true` with `Sql__ConnectionString` provided by Key Vault.

Deployment notes (short)
-----------------------
- `deploy-staging.ps1` and `deploy-production.ps1` create Azure resources (Storage account + container, Azure SQL, Key Vault, App Service) and set app settings that reference Key Vault secrets.
- The code expects the app setting `Sql__ConnectionString` to be available (the DB connection string) when `UseSqlServer` is true.

Where to start reading code
--------------------------
1. `src/FileService.Api/Program.cs` — startup, DI, and endpoint wiring (single-file entry point for API behavior).
2. `src/FileService.Core/Interfaces/*` — the application contracts (good to understand data flows).
3. `src/FileService.Infrastructure/Storage/*` — storage implementations and options.
4. `tests/FileService.Tests/Integration/*` — shows end-to-end expectations and example usage patterns.

If you want, I can also:
- generate a more detailed per-file map (calling out every source file) — larger but good for full onboarding, or
- add cross-reference links in `CODEBASE.md` to the most important source files.

Last updated: October 15, 2025
