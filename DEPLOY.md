# Deployment Guide

This document explains how to deploy the File Service across three environments: Development (local), Staging, and Production. It covers local dev, Azure multi-environment provisioning, configuration management, and CI/CD pipeline with proper promotion workflows.

---
## 1. Environment Overview

The service supports three deployment environments:

### **🔧 Development (Local)**
- **Purpose**: Local development and testing
- **Database**: SQLite or In-Memory Repository
- **Storage**: Stub implementation (no Azure dependencies)
- **Auth**: Development bypass (`?devUser=alice`)
- **URL**: `http://localhost:5090`

### **🧪 Staging**
- **Purpose**: Pre-production testing, integration testing, UAT
- **Database**: Azure SQL Database (smaller tier)
- **Storage**: Azure Blob Storage (separate container)
- **Auth**: External auth integration (test instance or optional)
- **URL**: `https://filesvc-api-staging.azurewebsites.net`

### **🚀 Production**
- **Purpose**: Live production workloads
- **Database**: Azure SQL Database (production tier)
- **Storage**: Azure Blob Storage (production container)
- **Auth**: External auth integration (production instance or optional)
- **URL**: `https://filesvc-api-prod.azurewebsites.net`

---
## 2. Environment Configuration Matrix

## 2. Environment Configuration Matrix

| Setting | Development | Staging | Production |
|---------|-------------|---------|------------|
| `ASPNETCORE_ENVIRONMENT` | Development | Staging | Production |
| `EnvironmentMode` | Development | Staging | Production |
| `BlobStorage__UseLocalStub` | true | false | false |
| `BlobStorage__ConnectionString` | (empty) | Key Vault Reference | Key Vault Reference |
| `BlobStorage__ContainerName` | userfiles | userfiles-staging | userfiles-prod |
| `Persistence__UseEf` | false (in-memory) | true | true |
| `Persistence__UseSqlServer` | false | true | true |
| `Sql__ConnectionString` | (unused) | Key Vault Reference | Key Vault Reference |
| **Database SKU** | N/A | Basic (5 DTU) | Standard S2 (50 DTU) |
| **App Service SKU** | N/A | B1 (Basic) | P1v2 (Premium) |
| **Monitoring Level** | Console only | Basic alerts | Full monitoring |
| **Backup Retention** | N/A | 7 days | 30 days |

> **Note**: Key Vault References use format: `@Microsoft.KeyVault(VaultName=<vault>;SecretName=<secret>)`

---
## 3. Multi-Environment Azure Architecture

```
Azure Subscription
├── 📂 file-svc-staging-rg          # Staging Resource Group
│   ├── 🌐 filesvc-api-staging      # Staging Web App
│   ├── 🗄️ filesvc-sql-staging      # Staging SQL Server
│   ├── 💾 filesvcstgstaging123     # Staging Storage Account
│   ├── 🔐 filesvc-kv-staging       # Staging Key Vault
│   └── 📊 filesvc-ai-staging       # Staging App Insights
│
└── 📂 file-svc-production-rg       # Production Resource Group
    ├── 🌐 filesvc-api-prod         # Production Web App
    ├── 🗄️ filesvc-sql-prod         # Production SQL Server
    ├── 💾 filesvcstgprod456        # Production Storage Account
    ├── 🔐 filesvc-kv-prod          # Production Key Vault
    └── 📊 filesvc-ai-prod          # Production App Insights
```

---
## 4. Configuration Keys Reference

## 4. Configuration Keys Reference

| Env Var / Key | Purpose | Dev Default | Staging | Production |
|---------------|---------|-------------|---------|------------|
| `ASPNETCORE_ENVIRONMENT` | ASP.NET environment mode | Development | Staging | Production |
| `EnvironmentMode` | App-specific environment features | Development | Staging | Production |
| `BlobStorage__UseLocalStub` | Use in-memory file bytes | true | false | false |
| `BlobStorage__ConnectionString` | Azure Storage connection | (empty) | Key Vault Ref | Key Vault Ref |
| `BlobStorage__ContainerName` | Container for user files | userfiles | userfiles-staging | userfiles-prod |
| `Persistence__UseEf` | EF Core enabled | false | true | true |
| `Persistence__UseSqlServer` | Use SQL Server instead of SQLite | false | true | true |
| `Sql__ConnectionString` | SQL Server connection | (unused) | Key Vault Ref | Key Vault Ref |
| `ApplicationInsights__InstrumentationKey` | AI monitoring | (unused) | Key Vault Ref | Key Vault Ref |
| `ExternalAuth__BaseUrl` | External auth API endpoint (optional) | (none) | test-auth.school.edu | auth.school.edu |
| `ExternalAuth__ApiKey` | External auth authentication key (optional) | (none) | Key Vault Ref | Key Vault Ref |

> **Note**: Double underscore (`__`) maps to nested JSON keys. Key Vault Ref = `@Microsoft.KeyVault(...)`

---
## 5. Local Development (No Changes)

1. Install .NET 8 SDK.
2. Run dev script:
   ```powershell
   cd scripts
   ./dev-run.ps1 -Port 5090 -SqlitePath dev-files.db
   ```
3. Upload test:
   ```powershell
   Invoke-RestMethod -Method Post -Uri 'http://localhost:5090/api/files/upload?devUser=demo1' -Form @{ file=Get-Item ..\README.md }
   ```
4. List:
   ```powershell
   Invoke-RestMethod -Method Get -Uri 'http://localhost:5090/api/files?devUser=demo1'
   ```

---
## 6. Staging Environment Deployment

### 6.1 Create Staging Resources

```powershell
# Staging Environment Variables
$env = "staging"
$resourceGroup = "file-svc-staging-rg"
$location = "eastus"
$storageAccount = "filesvcstg$(Get-Random)"
$webAppName = "filesvc-api-staging"
$keyVaultName = "filesvc-kv-staging"
$appInsightsName = "filesvc-ai-staging"
$sqlServerName = "filesvc-sql-staging"
$sqlDbName = "file-service-db"
$sqlAdminUser = "fsadmin"
$sqlAdminPassword = "StagingPassword123!" # Change this!

# Create staging resource group
Write-Host "Creating staging resource group..."
az group create -n $resourceGroup -l $location

# Create Application Insights
Write-Host "Creating Application Insights for staging..."
az monitor app-insights component create -a $appInsightsName -g $resourceGroup -l $location --application-type web
$aiKey = az monitor app-insights component show -a $appInsightsName -g $resourceGroup --query instrumentationKey -o tsv

# Create storage account
Write-Host "Creating staging storage account..."
az storage account create -n $storageAccount -g $resourceGroup -l $location --sku Standard_LRS --kind StorageV2
az storage container create --account-name $storageAccount -n userfiles-staging --auth-mode key --public-access off
$storageConnString = az storage account show-connection-string -n $storageAccount -g $resourceGroup -o tsv

# Create SQL Server and Database (Basic tier for staging)
Write-Host "Creating staging Azure SQL Database..."
az sql server create -n $sqlServerName -g $resourceGroup -l $location --admin-user $sqlAdminUser --admin-password $sqlAdminPassword
az sql db create -s $sqlServerName -g $resourceGroup -n $sqlDbName --service-objective Basic
az sql server firewall-rule create -s $sqlServerName -g $resourceGroup -n AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
$sqlConnString = az sql db show-connection-string -s $sqlServerName -n $sqlDbName -c ado.net | % { $_ -replace '<username>', $sqlAdminUser -replace '<password>', $sqlAdminPassword }

# Create Key Vault
Write-Host "Creating staging Key Vault..."
az keyvault create -n $keyVaultName -g $resourceGroup -l $location --enable-soft-delete true --enable-purge-protection true
az keyvault secret set --vault-name $keyVaultName -n BlobStorage--ConnectionString --value $storageConnString
az keyvault secret set --vault-name $keyVaultName -n Sql--ConnectionString --value $sqlConnString
az keyvault secret set --vault-name $keyVaultName -n ApplicationInsights--InstrumentationKey --value $aiKey
# (Optional) Set external auth secrets in Key Vault if you integrate with an external auth provider
# az keyvault secret set --vault-name $keyVaultName -n ExternalAuth--BaseUrl --value "https://test-auth.school.edu"
# az keyvault secret set --vault-name $keyVaultName -n ExternalAuth--ApiKey --value "staging-api-key-here"

# Create App Service (Basic tier for staging)
Write-Host "Creating staging App Service..."
az appservice plan create -n file-svc-staging-plan -g $resourceGroup --sku B1 --is-linux false
az webapp create -n $webAppName -g $resourceGroup -p file-svc-staging-plan --runtime "DOTNET|8.0"

# Configure Managed Identity
Write-Host "Configuring staging Managed Identity..."
az webapp identity assign -n $webAppName -g $resourceGroup
$principalId = az webapp identity show -n $webAppName -g $resourceGroup --query principalId -o tsv
az keyvault set-policy -n $keyVaultName -g $resourceGroup --object-id $principalId --secret-permissions get list
$storageId = az storage account show -n $storageAccount -g $resourceGroup --query id -o tsv
az role assignment create --assignee $principalId --role "Storage Blob Data Contributor" --scope $storageId

# Configure App Settings
Write-Host "Configuring staging App Settings..."
az webapp config appsettings set -n $webAppName -g $resourceGroup --settings `
  ASPNETCORE_ENVIRONMENT=Staging `
  EnvironmentMode=Staging `
  BlobStorage__UseLocalStub=false `
  "BlobStorage__ConnectionString=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=BlobStorage--ConnectionString)" `
  BlobStorage__ContainerName=userfiles-staging `
  Persistence__UseEf=true `
  Persistence__UseSqlServer=true `
  "Sql__ConnectionString=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=Sql--ConnectionString)" `
  "ApplicationInsights__InstrumentationKey=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=ApplicationInsights--InstrumentationKey)" `
  # Optional external auth Key Vault references (uncomment if used)
  # "ExternalAuth__BaseUrl=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=ExternalAuth--BaseUrl)" `
  # "ExternalAuth__ApiKey=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=ExternalAuth--ApiKey)"

# Security Settings
Write-Host "Applying staging security settings..."
az webapp config set -n $webAppName -g $resourceGroup --https-only true --min-tls-version 1.2

Write-Host "Staging deployment complete!"
Write-Host "Staging URL: https://$webAppName.azurewebsites.net"
```

---
## 7. Production Environment Deployment

### 7.1 Create Production Resources

```powershell
# Production Environment Variables
$env = "production"
$resourceGroup = "file-svc-production-rg"
$location = "eastus"
$storageAccount = "filesvcprd$(Get-Random)"
$webAppName = "filesvc-api-prod"
$keyVaultName = "filesvc-kv-prod"
$appInsightsName = "filesvc-ai-prod"
$sqlServerName = "filesvc-sql-prod"
$sqlDbName = "file-service-db"
$sqlAdminUser = "fsadmin"
$sqlAdminPassword = "ProductionPassword123!" # Change this!

# Create production resource group
Write-Host "Creating production resource group..."
az group create -n $resourceGroup -l $location

# Create Application Insights
Write-Host "Creating Application Insights for production..."
az monitor app-insights component create -a $appInsightsName -g $resourceGroup -l $location --application-type web
$aiKey = az monitor app-insights component show -a $appInsightsName -g $resourceGroup --query instrumentationKey -o tsv

# Create storage account with enhanced settings
Write-Host "Creating production storage account..."
az storage account create -n $storageAccount -g $resourceGroup -l $location --sku Standard_GRS --kind StorageV2 --enable-versioning true
az storage container create --account-name $storageAccount -n userfiles-prod --auth-mode key --public-access off
# Enable soft delete for production
az storage account blob-service-properties update --account-name $storageAccount --enable-delete-retention true --delete-retention-days 30
$storageConnString = az storage account show-connection-string -n $storageAccount -g $resourceGroup -o tsv

# Create SQL Server and Database (Standard tier for production)
Write-Host "Creating production Azure SQL Database..."
az sql server create -n $sqlServerName -g $resourceGroup -l $location --admin-user $sqlAdminUser --admin-password $sqlAdminPassword
az sql db create -s $sqlServerName -g $resourceGroup -n $sqlDbName --service-objective S2
az sql server firewall-rule create -s $sqlServerName -g $resourceGroup -n AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
# Configure automated backups for production
az sql db update -s $sqlServerName -n $sqlDbName -g $resourceGroup --backup-storage-redundancy Zone
$sqlConnString = az sql db show-connection-string -s $sqlServerName -n $sqlDbName -c ado.net | % { $_ -replace '<username>', $sqlAdminUser -replace '<password>', $sqlAdminPassword }

# Create Key Vault with enhanced security
Write-Host "Creating production Key Vault..."
az keyvault create -n $keyVaultName -g $resourceGroup -l $location --enable-soft-delete true --enable-purge-protection true --enable-rbac-authorization false
az keyvault secret set --vault-name $keyVaultName -n BlobStorage--ConnectionString --value $storageConnString
az keyvault secret set --vault-name $keyVaultName -n Sql--ConnectionString --value $sqlConnString
az keyvault secret set --vault-name $keyVaultName -n ApplicationInsights--InstrumentationKey --value $aiKey
# Optional: set external auth provider secrets in Key Vault if integrating with an external auth provider
# az keyvault secret set --vault-name $keyVaultName -n ExternalAuth--BaseUrl --value "https://auth.school.edu"
# az keyvault secret set --vault-name $keyVaultName -n ExternalAuth--ApiKey --value "production-api-key-here"

# Create App Service (Premium tier for production)
Write-Host "Creating production App Service..."
az appservice plan create -n file-svc-production-plan -g $resourceGroup --sku P1v2 --is-linux false
az webapp create -n $webAppName -g $resourceGroup -p file-svc-production-plan --runtime "DOTNET|8.0"

# Configure Managed Identity
Write-Host "Configuring production Managed Identity..."
az webapp identity assign -n $webAppName -g $resourceGroup
$principalId = az webapp identity show -n $webAppName -g $resourceGroup --query principalId -o tsv
az keyvault set-policy -n $keyVaultName -g $resourceGroup --object-id $principalId --secret-permissions get list
$storageId = az storage account show -n $storageAccount -g $resourceGroup --query id -o tsv
az role assignment create --assignee $principalId --role "Storage Blob Data Contributor" --scope $storageId

# Configure App Settings
Write-Host "Configuring production App Settings..."
az webapp config appsettings set -n $webAppName -g $resourceGroup --settings `
  ASPNETCORE_ENVIRONMENT=Production `
  EnvironmentMode=Production `
  BlobStorage__UseLocalStub=false `
  "BlobStorage__ConnectionString=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=BlobStorage--ConnectionString)" `
  BlobStorage__ContainerName=userfiles-prod `
  Persistence__UseEf=true `
  Persistence__UseSqlServer=true `
  "Sql__ConnectionString=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=Sql--ConnectionString)" `
  "ApplicationInsights__InstrumentationKey=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=ApplicationInsights--InstrumentationKey)" `
  # Optional External Auth Key Vault references (uncomment if used)
  # "ExternalAuth__BaseUrl=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=ExternalAuth--BaseUrl)" `
  # "ExternalAuth__ApiKey=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=ExternalAuth--ApiKey)"

# Enhanced security settings for production
Write-Host "Applying production security settings..."
az webapp config set -n $webAppName -g $resourceGroup --https-only true --min-tls-version 1.2 --ftps-state Disabled

Write-Host "Production deployment complete!"
Write-Host "Production URL: https://$webAppName.azurewebsites.net"
```

---
## 8. Deployment Automation Scripts

Create these PowerShell scripts in your `scripts/` folder:

### 8.1 `deploy-staging.ps1`
```powershell
param(
    [switch]$CreateResources,
    [switch]$DeployApp,
    [switch]$RunMigrations
)

$resourceGroup = "file-svc-staging-rg"
$webAppName = "filesvc-api-staging"

if ($CreateResources) {
    Write-Host "Creating staging Azure resources..."
    # Insert staging resource creation script from section 6.1
}

if ($DeployApp) {
    Write-Host "Deploying application to staging..."
    dotnet publish src/FileService.Api/FileService.Api.csproj -c Release -o publish-staging
    
    # Create migration bundle
    dotnet ef migrations bundle -p src/FileService.Infrastructure/FileService.Infrastructure.csproj -s src/FileService.Api/FileService.Api.csproj -o publish-staging/efbundle.exe
    
    cd publish-staging
    Compress-Archive * ../deploy-staging.zip -Force
    az webapp deploy -g $resourceGroup -n $webAppName --src-path ../deploy-staging.zip --type zip
    cd ..
}

if ($RunMigrations) {
    Write-Host "Running database migrations on staging..."
    # Get connection string from Key Vault and run migrations
    $connectionString = az keyvault secret show --vault-name "filesvc-kv-staging" --name "Sql--ConnectionString" --query value -o tsv
    ./publish-staging/efbundle.exe --connection $connectionString
}

Write-Host "Staging deployment completed!"
Write-Host "Staging URL: https://filesvc-api-staging.azurewebsites.net/swagger"
```

### 8.2 `deploy-production.ps1`
```powershell
param(
    [switch]$CreateResources,
    [switch]$DeployApp,
    [switch]$RunMigrations,
    [switch]$PromoteFromStaging
)

$resourceGroup = "file-svc-production-rg"
$webAppName = "filesvc-api-prod"

if ($CreateResources) {
    Write-Host "Creating production Azure resources..."
    # Insert production resource creation script from section 7.1
}

if ($PromoteFromStaging) {
    Write-Host "Promoting staging build to production..."
    # Use the same build artifact from staging
    Copy-Item "deploy-staging.zip" "deploy-production.zip"
} elseif ($DeployApp) {
    Write-Host "Creating fresh production build..."
    dotnet publish src/FileService.Api/FileService.Api.csproj -c Release -o publish-production
    
    # Create migration bundle
    dotnet ef migrations bundle -p src/FileService.Infrastructure/FileService.Infrastructure.csproj -s src/FileService.Api/FileService.Api.csproj -o publish-production/efbundle.exe
    
    cd publish-production
    Compress-Archive * ../deploy-production.zip -Force
    cd ..
}

if ($DeployApp -or $PromoteFromStaging) {
    Write-Host "Deploying to production..."
    az webapp deploy -g $resourceGroup -n $webAppName --src-path deploy-production.zip --type zip
}

if ($RunMigrations) {
    Write-Host "Running database migrations on production..."
    $connectionString = az keyvault secret show --vault-name "filesvc-kv-prod" --name "Sql--ConnectionString" --query value -o tsv
    ./publish-production/efbundle.exe --connection $connectionString
}

Write-Host "Production deployment completed!"
Write-Host "Production URL: https://filesvc-api-prod.azurewebsites.net/swagger"
```

---
## 9. CI/CD Pipeline for Multi-Environment

Resources (baseline):
1. Resource Group
2. Storage Account (Blob)
3. App Service Plan + Web App
4. (Optional now) Azure SQL Server + Database (later when replacing SQLite)
5. Key Vault (recommended for secrets)
6. Application Insights (telemetry)

### 4.1 Create Resource Group
```powershell
az group create -n file-svc-rg -l eastus
```

### 4.2 Storage Account
```powershell
az storage account create -n <storageacctname> -g file-svc-rg -l eastus --sku Standard_LRS --kind StorageV2
az storage container create --account-name <storageacctname> -n userfiles --auth-mode key --public-access off
```
Retrieve connection string:
```powershell
az storage account show-connection-string -n <storageacctname> -g file-svc-rg -o tsv
```

### 4.3 App Service
```powershell
az appservice plan create -n file-svc-plan -g file-svc-rg --sku P1v2 --is-linux false
az webapp create -n <appname-filesvc> -g file-svc-rg -p file-svc-plan --runtime "DOTNET|8.0"
```

### 4.4 Application Insights (Monitoring)
```powershell
az monitor app-insights component create -a file-svc-ai -g file-svc-rg -l eastus --application-type web
```
Get instrumentation key:
```powershell
az monitor app-insights component show -a file-svc-ai -g file-svc-rg --query instrumentationKey -o tsv
```

### 4.5 Azure SQL Database (Production Database)
```powershell
# Create SQL Server
az sql server create -n <sql-server-name> -g file-svc-rg -l eastus --admin-user <admin-user> --admin-password <admin-password>

# Create Database
az sql db create -s <sql-server-name> -g file-svc-rg -n file-service-db --service-objective Basic

# Configure firewall (allow Azure services)
az sql server firewall-rule create -s <sql-server-name> -g file-svc-rg -n AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
```
Get connection string:
```powershell
az sql db show-connection-string -s <sql-server-name> -n file-service-db -c ado.net
```

### 4.6 Key Vault (Secure Secrets Management)
```powershell
az keyvault create -n <kv-name> -g file-svc-rg -l eastus --enable-soft-delete true --enable-purge-protection true
az keyvault secret set --vault-name <kv-name> -n BlobStorage--ConnectionString --value "<storage-conn-string>"
az keyvault secret set --vault-name <kv-name> -n Sql--ConnectionString --value "<sql-conn-string>"
az keyvault secret set --vault-name <kv-name> -n ApplicationInsights--InstrumentationKey --value "<ai-key>"
```

### 4.7 Managed Identity (Secure Access)
```powershell
# Enable system-assigned managed identity for the web app
az webapp identity assign -n <appname-filesvc> -g file-svc-rg

# Get the principal ID
$principalId = az webapp identity show -n <appname-filesvc> -g file-svc-rg --query principalId -o tsv

# Grant Key Vault access to the managed identity
az keyvault set-policy -n <kv-name> -g file-svc-rg --object-id $principalId --secret-permissions get list

# Grant Storage Blob Data Contributor role to managed identity
$storageId = az storage account show -n <storageacctname> -g file-svc-rg --query id -o tsv
az role assignment create --assignee $principalId --role "Storage Blob Data Contributor" --scope $storageId
```

### 4.8 App Settings (Production with Key Vault References)
```powershell
az webapp config appsettings set -n <appname-filesvc> -g file-svc-rg --settings \
  EnvironmentMode=Production \
  BlobStorage__UseLocalStub=false \
  BlobStorage__ConnectionString="@Microsoft.KeyVault(VaultName=<kv-name>;SecretName=BlobStorage--ConnectionString)" \
  BlobStorage__ContainerName=userfiles \
  Persistence__UseEf=true \
  Persistence__UseSqlServer=true \
  Sql__ConnectionString="@Microsoft.KeyVault(VaultName=<kv-name>;SecretName=Sql--ConnectionString)" \
  ApplicationInsights__InstrumentationKey="@Microsoft.KeyVault(VaultName=<kv-name>;SecretName=ApplicationInsights--InstrumentationKey)" \
  ASPNETCORE_ENVIRONMENT=Production
```

> Later replace direct connection string with a Key Vault reference or Managed Identity (using `@Microsoft.KeyVault(...)`).

### 4.9 Deploy (zip deploy with production settings)
From repository root (after build):
```powershell
# Build for production
dotnet publish src/FileService.Api/FileService.Api.csproj -c Release -o publish

# Add database migration to deployment
dotnet ef migrations bundle -p src/FileService.Infrastructure/FileService.Infrastructure.csproj -s src/FileService.Api/FileService.Api.csproj -o publish/efbundle.exe

cd publish
Compress-Archive * ../deploy.zip -Force
az webapp deploy -g file-svc-rg -n <appname-filesvc> --src-path ../deploy.zip --type zip

# Run database migrations (if using SQL Server)
# Option 1: Run efbundle remotely via Kudu console at https://<appname-filesvc>.scm.azurewebsites.net/DebugConsole
# Option 2: Use connection string locally to migrate
# dotnet ef database update -s src/FileService.Api/FileService.Api.csproj --connection "<sql-connection-string>"
```

### 4.10 Security Hardening
```powershell
# Enable HTTPS only
az webapp config set -n <appname-filesvc> -g file-svc-rg --https-only true

# Configure minimum TLS version
az webapp config set -n <appname-filesvc> -g file-svc-rg --min-tls-version 1.2

# Add security headers (via web.config or startup configuration)
# Disable detailed error pages in production (handled in code via ASPNETCORE_ENVIRONMENT)
```

### 4.11 Monitoring & Alerting Setup
```powershell
# Create alert for high error rate
az monitor metrics alert create \
  --name "FileService-HighErrorRate" \
  --resource-group file-svc-rg \
  --scopes "/subscriptions/<subscription-id>/resourceGroups/file-svc-rg/providers/Microsoft.Web/sites/<appname-filesvc>" \
  --condition "count 'Http Server Errors' 'Http5xx' 'Total' > 10" \
  --description "Alert when HTTP 5xx errors exceed 10 in 5 minutes" \
  --evaluation-frequency 5m \
  --window-size 5m \
  --severity 2

# Create alert for high response time
az monitor metrics alert create \
  --name "FileService-HighResponseTime" \
  --resource-group file-svc-rg \
  --scopes "/subscriptions/<subscription-id>/resourceGroups/file-svc-rg/providers/Microsoft.Web/sites/<appname-filesvc>" \
  --condition "average 'Response Time' 'AverageResponseTime' 'Total' > 5000" \
  --description "Alert when average response time exceeds 5 seconds" \
  --evaluation-frequency 5m \
  --window-size 15m \
  --severity 3
```

Browse: `https://<appname-filesvc>.azurewebsites.net/swagger`

---
## 5. CI/CD (GitHub Actions Outline)

Create `.github/workflows/deploy.yml` (future):

```yaml
name: Build & Deploy
on:
  push:
    branches: [ main ]

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Restore
        run: dotnet restore
      - name: Build
        run: dotnet build --configuration Release --no-restore
      - name: Test
        run: dotnet test --configuration Release --no-build --verbosity normal
      - name: Publish
        run: dotnet publish src/FileService.Api/FileService.Api.csproj -c Release -o publish
      - name: Zip
        run: Compress-Archive -Path publish/* -DestinationPath deploy.zip
      - name: Deploy
        uses: azure/webapps-deploy@v3
        with:
          app-name: ${{ secrets.AZURE_WEBAPP_NAME }}
          publish-profile: ${{ secrets.AZURE_PUBLISH_PROFILE }}
          package: deploy.zip
```

Secrets needed:
- `AZURE_PUBLISH_PROFILE` (export from Portal > Web App > Get publish profile)
- `AZURE_WEBAPP_NAME`

> Future improvement: Use OIDC + `azure/login@v2` + `az webapp deploy` instead of publish profile.

---
## 6. Complete Production Deployment Script

Here's a comprehensive script that creates all necessary Azure resources:

```powershell
# Variables - Replace with your values
$resourceGroup = "file-svc-rg"
$location = "eastus"
$storageAccount = "filesvcstg$(Get-Random)"
$webAppName = "filesvc-api-$(Get-Random)"
$keyVaultName = "filesvc-kv-$(Get-Random)"
$appInsightsName = "filesvc-ai"
$sqlServerName = "filesvc-sql-$(Get-Random)"
$sqlDbName = "file-service-db"
$sqlAdminUser = "fsadmin"
$sqlAdminPassword = "YourSecurePassword123!" # Change this!

# Create resource group
Write-Host "Creating resource group..."
az group create -n $resourceGroup -l $location

# Create Application Insights
Write-Host "Creating Application Insights..."
az monitor app-insights component create -a $appInsightsName -g $resourceGroup -l $location --application-type web
$aiKey = az monitor app-insights component show -a $appInsightsName -g $resourceGroup --query instrumentationKey -o tsv

# Create storage account
Write-Host "Creating storage account..."
az storage account create -n $storageAccount -g $resourceGroup -l $location --sku Standard_LRS --kind StorageV2
az storage container create --account-name $storageAccount -n userfiles --auth-mode key --public-access off
$storageConnString = az storage account show-connection-string -n $storageAccount -g $resourceGroup -o tsv

# Create SQL Server and Database
Write-Host "Creating Azure SQL Database..."
az sql server create -n $sqlServerName -g $resourceGroup -l $location --admin-user $sqlAdminUser --admin-password $sqlAdminPassword
az sql db create -s $sqlServerName -g $resourceGroup -n $sqlDbName --service-objective Basic
az sql server firewall-rule create -s $sqlServerName -g $resourceGroup -n AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
$sqlConnString = az sql db show-connection-string -s $sqlServerName -n $sqlDbName -c ado.net | % { $_ -replace '<username>', $sqlAdminUser -replace '<password>', $sqlAdminPassword }

# Create Key Vault
Write-Host "Creating Key Vault..."
az keyvault create -n $keyVaultName -g $resourceGroup -l $location --enable-soft-delete true --enable-purge-protection true
az keyvault secret set --vault-name $keyVaultName -n BlobStorage--ConnectionString --value $storageConnString
az keyvault secret set --vault-name $keyVaultName -n Sql--ConnectionString --value $sqlConnString
az keyvault secret set --vault-name $keyVaultName -n ApplicationInsights--InstrumentationKey --value $aiKey

# Create App Service
Write-Host "Creating App Service..."
az appservice plan create -n file-svc-plan -g $resourceGroup --sku P1v2 --is-linux false
az webapp create -n $webAppName -g $resourceGroup -p file-svc-plan --runtime "DOTNET|8.0"

# Configure Managed Identity
Write-Host "Configuring Managed Identity..."
az webapp identity assign -n $webAppName -g $resourceGroup
$principalId = az webapp identity show -n $webAppName -g $resourceGroup --query principalId -o tsv
az keyvault set-policy -n $keyVaultName -g $resourceGroup --object-id $principalId --secret-permissions get list
$storageId = az storage account show -n $storageAccount -g $resourceGroup --query id -o tsv
az role assignment create --assignee $principalId --role "Storage Blob Data Contributor" --scope $storageId

# Configure App Settings
Write-Host "Configuring App Settings..."
az webapp config appsettings set -n $webAppName -g $resourceGroup --settings `
  EnvironmentMode=Production `
  BlobStorage__UseLocalStub=false `
  "BlobStorage__ConnectionString=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=BlobStorage--ConnectionString)" `
  BlobStorage__ContainerName=userfiles `
  Persistence__UseEf=true `
  Persistence__UseSqlServer=true `
  "Sql__ConnectionString=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=Sql--ConnectionString)" `
  "ApplicationInsights__InstrumentationKey=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=ApplicationInsights--InstrumentationKey)" `
  ASPNETCORE_ENVIRONMENT=Production

# Security Settings
Write-Host "Applying security settings..."
az webapp config set -n $webAppName -g $resourceGroup --https-only true --min-tls-version 1.2

Write-Host "Deployment complete!"
Write-Host "Web App URL: https://$webAppName.azurewebsites.net"
Write-Host "Storage Account: $storageAccount"
Write-Host "SQL Server: $sqlServerName.database.windows.net"
Write-Host "Key Vault: $keyVaultName"
```

## 7. Database Migrations for Production
## 7. Database Migrations for Production

### Add SQL Server Support to the Project
1. **Add SQL Server Package**:
```powershell
cd src/FileService.Infrastructure
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
```

2. **Update Program.cs** to support SQL Server:
```csharp
// Add after the existing EF configuration
if (builder.Configuration.GetValue<bool>("Persistence:UseSqlServer"))
{
    var sqlConnection = builder.Configuration.GetConnectionString("Sql");
    builder.Services.AddDbContext<FileServiceDbContext>(options =>
        options.UseSqlServer(sqlConnection));
}
```

3. **Create and Apply Migrations**:
```powershell
# Generate initial migration
dotnet ef migrations add InitialCreate -p src/FileService.Infrastructure/FileService.Infrastructure.csproj -s src/FileService.Api/FileService.Api.csproj

# Create migration bundle for deployment
dotnet ef migrations bundle -p src/FileService.Infrastructure/FileService.Infrastructure.csproj -s src/FileService.Api/FileService.Api.csproj -o efbundle.exe

# Apply migrations to production database
./efbundle.exe --connection "<sql-connection-string>"
```

## 8. Infrastructure as Code (Optional Enhancement)

Create `azure-resources.bicep` for reproducible deployments:

```bicep
param location string = resourceGroup().location
param appName string
param sqlAdminPassword string

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-04-01' = {
  name: '${appName}stg${uniqueString(resourceGroup().id)}'
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
}

resource sqlServer 'Microsoft.Sql/servers@2021-02-01-preview' = {
  name: '${appName}-sql-${uniqueString(resourceGroup().id)}'
  location: location
  properties: {
    administratorLogin: 'fsadmin'
    administratorLoginPassword: sqlAdminPassword
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2021-01-15' = {
  name: '${appName}-plan'
  location: location
  sku: { name: 'P1v2', tier: 'PremiumV2' }
}

resource webApp 'Microsoft.Web/sites@2021-01-15' = {
  name: appName
  location: location
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      netFrameworkVersion: 'v8.0'
      metadata: [{ name: 'CURRENT_STACK', value: 'dotnet' }]
    }
  }
  identity: { type: 'SystemAssigned' }
}
```

Deploy with:
```powershell
az deployment group create -g file-svc-rg --template-file azure-resources.bicep --parameters appName=filesvc-api sqlAdminPassword=YourSecurePassword123!
```

## 9. Production Deployment Checklist

### Pre-Deployment
- [ ] Update `appsettings.Production.json` with production settings
- [ ] Test application locally with production-like configuration
- [ ] Run all unit tests: `dotnet test`
- [ ] Run smoke tests against staging environment
- [ ] Verify all secrets are stored in Key Vault
- [ ] Review security settings and access controls

### Deployment
- [ ] Create all Azure resources using the script above
- [ ] Build and deploy application: `dotnet publish` + `az webapp deploy`
- [ ] Run database migrations: `./efbundle.exe`
- [ ] Verify managed identity permissions
- [ ] Test Key Vault secret access

### Post-Deployment
- [ ] Verify `/swagger` endpoint is accessible
- [ ] Test file upload via Swagger UI
- [ ] Verify files are stored in Azure Blob Storage
- [ ] Test file download and delete operations
- [ ] Monitor Application Insights for errors
- [ ] Set up alerts for critical metrics
- [ ] Configure backup policies for SQL Database

### External Authentication (Production)
- [ ] Replace dev authentication bypass with real external auth integration if required
- [ ] Implement proper token or HMAC validation as appropriate for your provider
- [ ] Configure external auth server endpoints and credentials (store in Key Vault)
- [ ] Test user authentication and role-based access
- [ ] Set up audit logging for authentication events

## 10. Monitoring & Maintenance

### Application Insights Queries
```kusto
// Monitor error rates
requests
| where timestamp > ago(1h)
| summarize requests = count(), failures = countif(success == false) by bin(timestamp, 5m)
| extend failureRate = failures * 100.0 / requests

// Track file operations
customEvents
| where name in ("FileUploaded", "FileDeleted", "FileDownloaded")
| summarize count() by name, bin(timestamp, 1h)
```

### Database Monitoring
```sql
-- Monitor database size
SELECT 
    DB_NAME() as DatabaseName,
    SUM(size) * 8 / 1024 as DatabaseSizeInMB
FROM sys.database_files;

-- Monitor file operations
SELECT 
    COUNT(*) as TotalFiles,
    SUM(FileSizeBytes) / 1024 / 1024 as TotalSizeInMB,
    CreatedBy,
    COUNT(CASE WHEN CreatedAt > DATEADD(day, -1, GETDATE()) THEN 1 END) as FilesLast24Hours
FROM FileRecords
GROUP BY CreatedBy;
```

## 11. Backup & Disaster Recovery

### Database Backups
```powershell
# Configure automated backups (7-day retention)
az sql db update -s <sql-server-name> -n <db-name> -g file-svc-rg --backup-storage-redundancy Local

# Manual backup
az sql db export -s <sql-server-name> -n <db-name> -g file-svc-rg --admin-user <admin-user> --admin-password <password> --storage-key-type StorageAccessKey --storage-key <storage-key> --storage-uri "https://<storage-account>.blob.core.windows.net/backups/backup-$(Get-Date -Format 'yyyyMMdd-HHmmss').bacpac"
```

### Blob Storage Backups
```powershell
# Enable soft delete for blobs (30-day retention)
az storage account blob-service-properties update --account-name <storage-account> --enable-delete-retention true --delete-retention-days 30

# Enable versioning
az storage account blob-service-properties update --account-name <storage-account> --enable-versioning true
```

---

## Summary

The deployment guide now provides:

✅ **Complete Azure Resource Provisioning** - All necessary services  
✅ **Security Best Practices** - Managed Identity, Key Vault, HTTPS  
✅ **Production Database** - Azure SQL with migrations  
✅ **Monitoring & Alerting** - Application Insights with custom metrics  
✅ **Infrastructure as Code** - Bicep templates for reproducible deployments  
✅ **Comprehensive Checklist** - Pre/post deployment validation  
✅ **Backup & Recovery** - Database and blob storage protection  

This guide ensures a production-ready deployment with enterprise-grade security, monitoring, and reliability! 🚀

## 13. Troubleshooting Multi-Environment Issues

| Environment | Symptom | Possible Cause | Fix |
|-------------|---------|----------------|-----|
| **Staging** | 401 responses | Missing auth headers | Use external auth headers or check staging credentials |
| **Staging** | Upload works but download 404 | Wrong container name | Verify `userfiles-staging` container exists |
| **Staging** | Database connection fails | Key Vault access issue | Check managed identity permissions |
| **Production** | Swagger disabled | Production config | Expected behavior - use staging for API testing |
| **Production** | High memory usage | Large file processing | Monitor App Insights and consider scaling up |
| **Both** | Key Vault secret not found | Wrong secret name format | Use exact format: `BlobStorage--ConnectionString` |
| **Both** | SQL timeout | Database overloaded | Scale up database tier or optimize queries |

### Common Resolution Steps:

#### 1. Key Vault Access Issues
```powershell
# Check managed identity permissions
az webapp identity show -n <webapp-name> -g <resource-group>
az keyvault show -n <keyvault-name> -g <resource-group> --query properties.accessPolicies
```

#### 2. Database Connection Problems
```powershell
# Test SQL connection manually
az sql db show -s <sql-server> -n <db-name> -g <resource-group>
az sql server firewall-rule list -s <sql-server> -g <resource-group>
```

#### 3. Storage Access Issues
```powershell
# Check storage container and permissions
az storage container show --account-name <storage-account> -n <container-name>
az role assignment list --assignee <managed-identity-principal-id> --scope <storage-account-resource-id>
```

---
## 14. Environment Management Best Practices

### 14.1 Configuration Management
- ✅ **Use Key Vault** for all secrets and connection strings
- ✅ **Environment-specific app settings** in `appsettings.{Environment}.json`
- ✅ **Managed Identity** instead of service principal credentials
- ✅ **Different resource groups** for complete isolation
- ✅ **Naming conventions** that clearly identify environment

### 14.2 Security Best Practices
- ✅ **Staging**: Basic security, real auth, test data only
- ✅ **Production**: Enhanced security, full monitoring, real data
- ✅ **Network isolation**: Consider VNet integration for production
- ✅ **Access control**: Different Azure AD groups for staging/production access
- ✅ **Audit logging**: Track all administrative actions

### 14.3 Cost Optimization
```powershell
# Staging: Use smaller SKUs
# App Service: B1 ($13/month)
# SQL Database: Basic ($5/month)
# Storage: Standard_LRS

# Production: Use appropriate SKUs
# App Service: P1v2 ($73/month)
# SQL Database: S2 ($30/month)
# Storage: Standard_GRS (with backup redundancy)
```

### 14.4 Data Management
- 🔄 **Staging Data**: Use anonymized production data or synthetic test data
- 🔄 **Database Refresh**: Periodically refresh staging from production backup
- 🔄 **Data Retention**: Configure appropriate retention policies for each environment
- 🔄 **Cleanup**: Automated cleanup of old test data in staging

---
## 15. Quick Start Commands

### 15.1 Create Both Environments (One Command)
```powershell
# Create staging environment
.\scripts\deploy-staging.ps1 -CreateResources

# Create production environment  
.\scripts\deploy-production.ps1 -CreateResources

# Deploy application to both
.\scripts\deploy-staging.ps1 -DeployApp -RunMigrations
.\scripts\deploy-production.ps1 -DeployApp -RunMigrations
```

### 15.2 Daily Development Workflow
```powershell
# 1. Develop locally
.\scripts\dev-run.ps1

# 2. Deploy to staging for testing
.\scripts\deploy-staging.ps1 -DeployApp

# 3. Run staging tests
.\scripts\test-staging.ps1

# 4. Promote to production (after approval)
.\scripts\deploy-production.ps1 -PromoteFromStaging
```

### 15.3 Environment URLs
- **Local Development**: `http://localhost:5090/swagger`
- **Staging**: `https://filesvc-api-staging.azurewebsites.net/swagger`
- **Production**: `https://filesvc-api-prod.azurewebsites.net` (Swagger disabled)

---
## 16. Cost Estimation

### Monthly Azure Costs (Estimate)

| Service | Staging | Production | Notes |
|---------|---------|------------|-------|
| **App Service** | $13 (B1) | $73 (P1v2) | Staging can auto-scale down |
| **SQL Database** | $5 (Basic) | $30 (S2) | Production may need higher tier |
| **Storage Account** | $2 | $5 | Includes backup redundancy |
| **Application Insights** | $0-5 | $10-20 | Based on telemetry volume |
| **Key Vault** | $1 | $1 | Secret operations |
| **Total/Month** | **~$21** | **~$119** | Approximate costs |

### Cost Optimization Tips:
- 🔄 **Auto-shutdown staging** during non-business hours
- 🔄 **Scale down staging** App Service to B1 or even F1 (free tier)
- 🔄 **Use Basic SQL tier** for staging (adequate for testing)
- 🔄 **Monitor usage** with Azure Cost Management

---
## Summary

Your **multi-environment deployment** is now ready! 🎉

### ✅ What You Get:

1. **🔧 Local Development** - Fast iteration with in-memory storage
2. **🧪 Staging Environment** - Real Azure services for integration testing
3. **🚀 Production Environment** - Enterprise-grade deployment with full monitoring
4. **🔄 CI/CD Pipeline** - Automated deployment with proper promotion workflow
5. **📊 Monitoring & Alerting** - Environment-specific monitoring strategies
6. **💾 Backup & Recovery** - Production-grade data protection
7. **🛡️ Security** - Key Vault, Managed Identity, and proper access controls

### 🚀 Next Steps:

1. **Run the staging deployment script** to create your first environment
2. **Test the CI/CD pipeline** by pushing to the `develop` branch
3. **Configure monitoring alerts** for both environments
4. **Set up your external auth integration** with real credentials (if required)
5. **Train your team** on the promotion workflow

This architecture provides **enterprise-grade multi-environment deployment** with proper isolation, security, and operational practices! 🌟
