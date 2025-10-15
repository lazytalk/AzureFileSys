# Staging Deployment Checklist

This document provides a step-by-step checklist to provision and deploy the FileService app into an Azure *staging* environment. It mirrors the behavior of `scripts/deploy-staging.ps1` but is written as an operational checklist you can follow.

Prerequisites
-------------
- Azure CLI installed and authenticated (`az login`).
  - Note: for Azure China (21Vianet) use `az cloud set --name AzureChinaCloud` before `az login` if your subscription is in China.
- .NET 8 SDK and `dotnet-ef` tools available locally (for producing migration bundles when needed).
- Permissions in the target subscription to create Resource Groups, App Service, SQL server/databases, Key Vault, Storage accounts, and role assignments.

High-level steps
----------------
1. Review and adjust the deployment script parameters (resource prefix, location, password).
2. Create resources (resource group, storage account & container, SQL server & DB, Key Vault, App Insights, App Service plan & Web App).
3. Register secrets in Key Vault and grant access to the Web App managed identity.
4. Build & publish the application and deploy to App Service.
5. Create an EF migration bundle and run migrations against the staging DB.
6. Run smoke tests against the staging endpoint and verify results.

Operational checklist (commands you can copy)
--------------------------------------------

1) Authenticate and select subscription

```powershell
az login
az account set --subscription "<your-subscription-id>"
```

2) Choose parameters (example)

```powershell
$prefix = 'filesvc-stg'                    # resource name prefix to keep names consistent
$location = 'chinaeast2'                   # change to your preferred region
$sqlAdminPassword = '<replace-with-strong-password>'
```

3) Create resources (provisioning)

Run the parameterized script. This will create all required staging resources and place necessary secrets in Key Vault.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\deploy-staging.ps1 -CreateResources -SubscriptionId "<your-subscription-id>" -Location $location -ResourcePrefix $prefix -SqlAdminPassword $sqlAdminPassword
```

What this does (summary):
- Creates resource group and namespace using `$prefix`.
- Creates storage account and container `userfiles-staging`.
- Creates Azure SQL server and DB; stores the connection string in Key Vault as `Sql--ConnectionString`.
- Creates Key Vault and stores secrets: `BlobStorage--ConnectionString`, `Sql--ConnectionString`, `ApplicationInsights--InstrumentationKey`.
- Creates App Service plan & Web App (with managed identity); sets Key Vault access policy and role assignment for Storage.

4) Verify secrets & identity

```powershell
az keyvault secret list --vault-name "${prefix}-kv"
az webapp identity show -n "${prefix}-app" -g "${prefix}-rg"
az webapp config appsettings list -n "${prefix}-app" -g "${prefix}-rg"
```

5) Deploy the application

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\deploy-staging.ps1 -DeployApp -SubscriptionId "<your-subscription-id>" -Location $location -ResourcePrefix $prefix
```

6) Create and run EF migrations bundle

```powershell
# create bundle (on your machine)
dotnet ef migrations bundle -p src/FileService.Infrastructure/FileService.Infrastructure.csproj -s src/FileService.Api/FileService.Api.csproj -o publish-staging/efbundle.exe

# get connection string from Key Vault
$sqlConn = az keyvault secret show --vault-name "${prefix}-kv" --name Sql--ConnectionString --query value -o tsv

# execute bundle against staging DB
.\publish-staging\efbundle.exe --connection "$sqlConn"
```

7) Smoke test & verification

- Tail logs
```powershell
az webapp log tail -n "${prefix}-app" -g "${prefix}-rg"
```
- Test endpoints:
  - `https://<webApp>.azurewebsites.net/swagger` (if enabled)
  - `https://<webApp>.azurewebsites.net/Admin`

Sample API tests:
```powershell
# list files
curl -i https://<webApp>.azurewebsites.net/api/files

# upload
curl -i -F "file=@.\temp-upload.txt" https://<webApp>.azurewebsites.net/api/files/upload
```

Rollback & cleanup
-------------------
- To remove resources created during a test run:
```powershell
az group delete -n "${prefix}-rg" --yes --no-wait
```
- For DB schema rollback: restore from backup (create backup before applying production migrations).

Security notes
--------------
- Avoid hard-coded passwords. Use the `-SqlAdminPassword` parameter or store the admin password in a secure secret store.
- Use Managed Identity for App Service and grant minimum permissions.
- Use Key Vault references in App Settings so secrets are not stored directly in App Settings.

Advanced
--------
- For CI/CD, create a GitHub Action that calls the deploy script (or uses Azure WebApp deploy action) and runs the efbundle as part of the release pipeline.
- For data-copy between environments use bacpac or Azure Data Factory instead of EF migrations.

---
Last updated: October 15, 2025
