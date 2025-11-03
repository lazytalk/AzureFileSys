# Staging Deployment Checklist

This document provides a step-by-step checklist to provision and deploy the FileService app into an Azure *staging* environment. It mirrors the behavior of `scripts/deploy-staging.ps1` but is written as an operational checklist you can follow.

Prerequisites
-------------
 - Azure CLI installed and authenticated (`az login`).
   - Note: for Azure China (21Vianet) run `az cloud set --name AzureChinaCloud` and then `az login` (the cloud selection must happen before login or you must re-authenticate to access China endpoints).
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
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\deploy-staging.ps1 -CreateResources -SubscriptionId "<your-subscription-id>" -Location $location -ResourcePrefix $prefix -SqlAdminCredential $cred
```

Note: the script now helps with authentication and SQL credential prompting. If you run the script without pre-authenticating it will:

- Detect China locations and run `az cloud set --name AzureChinaCloud` automatically.
- If not authenticated for the selected cloud, it will run `az login` to prompt you to authenticate.
- If you don't pass an SQL credential parameter, the script will prompt using `Get-Credential` so the only thing you need to do is run the script and enter credentials interactively.

Resource group creation
-----------------------
- The script will create the resource group for you. Behavior:
  - If you run with `-CreateResources` the script creates the resource group as part of the provisioning flow.
  - If you run `-DeployApp` or `-RunMigrations` and the resource group is missing, the script will create the resource group just-in-time so dependent operations can proceed.

Credential example (interactive)
--------------------------------
If you want to pre-create a PSCredential and pass it to the script instead of being prompted during the run:

```powershell
$cred = Get-Credential -Message 'SQL admin credential (example: fsadmin)'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\deploy-staging.ps1 -CreateResources -SubscriptionId "<your-subscription-id>" -Location $location -ResourcePrefix $prefix -SqlAdminCredential $cred
```

What this does (summary):
- Creates resource group and namespace using `$prefix`.
- Creates storage account and container `userfiles-staging`.
- Creates Azure SQL server and DB; stores the connection string in Key Vault as `Sql--ConnectionString`.
- Creates Key Vault and stores secrets: `BlobStorage--ConnectionString`, `Sql--ConnectionString`, `ApplicationInsights--InstrumentationKey`.
- Creates App Service plan & Web App (with managed identity); sets Key Vault access policy and role assignment for Storage.

Azure China notes
-----------------
- Domain suffixes differ in Azure China. The App Service hostname will end with `.azurewebsites.cn` instead of `.azurewebsites.net`.
- Some global services or API versions differ in China; the script will set the Azure CLI cloud automatically when it detects a China location but you still must run `az login` for that cloud if not already authenticated.
- Storage and Key Vault have naming constraints: storage account names must be all lowercase, 3-24 characters, and only alphanumeric; the script sanitizes names automatically, but pick a short `ResourcePrefix` to avoid truncation.

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
