# Azure File Management Service (Initial Scaffold)

This repository contains an initial scaffold for a file management backend aligned with the target architecture (ASP.NET Core API + Azure Blob Storage + metadata + PowerSchool auth integration).

## Current State
- Manual project files created (no `dotnet new` CLI run here because .NET SDK not detected in environment yet).
- Minimal solution with projects:
  - `FileService.Api` (Minimal API endpoints for upload/list/get/delete)
  - `FileService.Core` (Entities, interfaces, DTOs)
  - `FileService.Infrastructure` (In-memory metadata + stub blob storage)
  - `FileService.Tests` (Basic unit test)
- Stub PowerSchool auth via headers: `X-PowerSchool-User`, optional `X-PowerSchool-Role` ("admin" for elevated privileges).

## Prerequisites
Install .NET 8 SDK (LTS) on your machine:
https://dotnet.microsoft.com/en-us/download/dotnet/8.0

Verify:
```powershell
dotnet --version
```

## Restore & Build
After installing the SDK:
```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/FileService.Api/FileService.Api.csproj
```

Navigate to Swagger UI (once real hosting or local run):
`https://localhost:5001/swagger` (port may vary; check console output)

## Example Requests

Upload (PowerShell):
```powershell
Invoke-RestMethod -Method Post -Uri https://localhost:5001/api/files/upload -Headers @{ 'X-PowerSchool-User'='teacher1'; 'X-PowerSchool-Role'='admin' } -Form @{ file= Get-Item .\sample.txt }
```

List own files:
```powershell
Invoke-RestMethod -Method Get -Uri 'https://localhost:5001/api/files' -Headers @{ 'X-PowerSchool-User'='teacher1' }
```

List all (admin only):
```powershell
Invoke-RestMethod -Method Get -Uri 'https://localhost:5001/api/files?all=true' -Headers @{ 'X-PowerSchool-User'='teacher1'; 'X-PowerSchool-Role'='admin' }
```

Get file (returns stub SAS URL):
```powershell
Invoke-RestMethod -Method Get -Uri 'https://localhost:5001/api/files/{id}' -Headers @{ 'X-PowerSchool-User'='teacher1' }
```

Delete file:
```powershell
Invoke-RestMethod -Method Delete -Uri 'https://localhost:5001/api/files/{id}' -Headers @{ 'X-PowerSchool-User'='teacher1' }
```

## Next Steps (Planned)
1. Replace stub storage with Azure Blob Storage implementation (configure connection string via Key Vault / app settings).
2. Add real database (Azure SQL or Cosmos DB) with EF Core migrations.
3. Implement robust PowerSchool token validation (API call / shared secret signature).
4. Add file type validation & antivirus scanning hook.
5. Add structured logging + Application Insights telemetry.
6. Implement pagination & search endpoints.
7. Add CI/CD (GitHub Actions) & IaC (Bicep/Terraform) for Azure provisioning.

## Environments & Configuration

The service distinguishes between Development and Production via the `EnvironmentMode` configuration key (and/or the standard `ASPNETCORE_ENVIRONMENT`).

### Files
- `src/FileService.Api/appsettings.json` (default, sets `EnvironmentMode` to `Development`).
- `src/FileService.Api/appsettings.Production.json` (overrides for production, sets `UseLocalStub=false`).

### Key Configuration Sections
| Key | Description | Dev Default | Prod Example |
|-----|-------------|-------------|--------------|
| `EnvironmentMode` | Custom environment flag used by code for dev-only features | `Development` | `Production` |
| `BlobStorage:UseLocalStub` | If true, in-memory stub storage used | true | false |
| `BlobStorage:ConnectionString` | Azure Storage connection string (required when stub disabled) | (empty) | `DefaultEndpointsProtocol=...` |
| `Persistence:UseEf` | Enables EF Core repository | true | true |
| `Persistence:SqlitePath` | SQLite DB file path (local dev) | `files.db` or supplied via script | Typically replaced by Azure SQL provider |

Override any value with environment variables (double underscore for nesting):
```
$env:EnvironmentMode='Production'
$env:BlobStorage__ConnectionString='...'
$env:BlobStorage__UseLocalStub='false'
```

### Development Conveniences
Active only when `EnvironmentMode=Development` (or config equals `Development`):
- Query parameter auth shortcut: `?devUser=<id>&role=admin` sets user context without headers.
- Token mimic endpoints:
  - `POST /dev/powerschool/token` (form/body: userId, role, optional secret) → returns mock token.
  - `POST /dev/powerschool/validate` (token, optional secret) → decodes & validates mock token.
- Stub blob storage keeps file bytes in-memory (lost on restart) while metadata persists in SQLite.

### Production Expectations
- Provide real Azure Blob connection string and set `BlobStorage:UseLocalStub=false`.
- Remove dev query parameter usage; send real `X-PowerSchool-User` (and future auth token header) only.
- Use Azure SQL / managed database instead of local SQLite (future provider switch via configuration).
- Harden: disable `/dev/powerschool/*` by ensuring `EnvironmentMode` is not `Development`.

### Dev Run Script
`scripts/dev-run.ps1` launches the API pre-configured for local development.

Usage:
```powershell
cd scripts
./dev-run.ps1 -Port 5090 -SqlitePath dev-files.db
```

Then test an upload (no headers needed due to dev shortcut):
```powershell
Invoke-RestMethod -Method Post -Uri 'http://localhost:5090/api/files/upload?devUser=demo1&role=admin' -Form @{ file=Get-Item ..\README.md }
```

List files:
```powershell
Invoke-RestMethod -Method Get -Uri 'http://localhost:5090/api/files?devUser=demo1'
```

### Switching to Production Locally (Example)
```powershell
$env:EnvironmentMode='Production'
$env:BlobStorage__ConnectionString='DefaultEndpointsProtocol=...'
$env:BlobStorage__UseLocalStub='false'
dotnet run --project src/FileService.Api/FileService.Api.csproj
```

The service will then use the real Azure Blob backend (if credentials permit SAS generation).

## Configuration (Future)
Environment variables expected (once implemented):
```
AZURE_STORAGE_CONNECTION_STRING=
FILES_CONTAINER_NAME=userfiles
POWERSCHOOL_AUTH_ENDPOINT=
POWERSCHOOL_SHARED_SECRET=
DB_CONNECTION_STRING=
```

## License
TBD
