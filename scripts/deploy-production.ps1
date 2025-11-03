#!/usr/bin/env pwsh
# deploy-production.ps1 - Deploy File Service to Azure Production Environment

param(
    [switch]$CreateResources,
    [switch]$DeployApp,
    [switch]$RunMigrations,
    [switch]$PromoteFromStaging,
    [string]$SubscriptionId = "",
    [string]$Location = "chinaeast2",  # Azure China region
    [string]$ResourcePrefix = "filesvc-prod",
    [object]$SqlAdminPassword = $null
)

function Assert-AzureCliReady {
    param(
        [string]$LocationHint
    )
    # Check az available
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        Write-Error "Azure CLI (az) is not available in PATH. Install Azure CLI and try again."
        exit 1
    }

    # Check cloud for China regions
    if ($LocationHint -and ($LocationHint -match 'china')) {
        $currentCloud = az cloud show --query name -o tsv 2>$null
        if ($currentCloud -ne 'AzureChinaCloud') {
            Write-Host "Azure CLI cloud is not set to AzureChinaCloud. Setting it now..." -ForegroundColor Yellow
            az cloud set --name AzureChinaCloud
            Write-Host "Note: you may need to run 'az login' for the China cloud if not already authenticated." -ForegroundColor Yellow
        }
    }
}

# Production Environment Variables
$resourceGroup = "${ResourcePrefix}-rg"
$storageAccount = "${ResourcePrefix}prd$(Get-Random -Minimum 1000 -Maximum 9999)"
$webAppName = "${ResourcePrefix}-app"
$keyVaultName = "${ResourcePrefix}-kv"
$appInsightsName = "${ResourcePrefix}-ai"
$sqlServerName = "${ResourcePrefix}-sql"
$sqlDbName = "file-service-db"
$sqlAdminUser = "fsadmin"

# Accept either a SecureString or a plain string for SqlAdminPassword.
# Prefer a SecureString (recommended). If a plain string is provided we'll convert it,
# and if nothing is provided we prompt interactively.
if ($SqlAdminPassword -is [System.Security.SecureString]) {
    $secureSqlAdminPassword = $SqlAdminPassword
} elseif ($SqlAdminPassword -is [string] -and $SqlAdminPassword) {
    Write-Warning "SqlAdminPassword was provided as plain text on the command line. This is insecure; prefer passing a SecureString or omitting to be prompted interactively."
    # Convert the plain-text string to a SecureString for internal use
    $secureSqlAdminPassword = ConvertTo-SecureString -String $SqlAdminPassword -AsPlainText -Force
} else {
    Write-Host "No SQL admin password supplied; prompting interactively (secure)."
    $secureSqlAdminPassword = Read-Host -AsSecureString "Enter SQL admin password (hidden)"
}

# Convert SecureString to plain text securely for az CLI usage, then zero the pointer
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureSqlAdminPassword)
$unsecureSqlAdminPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)

Write-Host "🚀 File Service - Production Deployment" -ForegroundColor Magenta
Write-Host "=======================================" -ForegroundColor Magenta

if ($SubscriptionId) {
    Write-Host "Setting Azure subscription: $SubscriptionId"
    az account set --subscription $SubscriptionId
}

# Ensure Azure CLI is ready (cloud/login) for the selected location
Assert-AzureCliReady -LocationHint $Location

if ($CreateResources) {
    Write-Host "📦 Creating Azure resources for production environment..." -ForegroundColor Yellow
    
    # Create production resource group
    Write-Host "Creating resource group: $resourceGroup"
    az group create -n $resourceGroup -l $Location

    # Create Application Insights
    Write-Host "Creating Application Insights: $appInsightsName"
    az monitor app-insights component create -a $appInsightsName -g $resourceGroup -l $Location --application-type web
    $aiKey = az monitor app-insights component show -a $appInsightsName -g $resourceGroup --query instrumentationKey -o tsv

    # Create storage account with enhanced settings
    Write-Host "Creating production storage account: $storageAccount"
    az storage account create -n $storageAccount -g $resourceGroup -l $Location --sku Standard_GRS --kind StorageV2 --enable-versioning true
    az storage container create --account-name $storageAccount -n userfiles-prod --auth-mode key --public-access off
    # Enable soft delete for production
    az storage account blob-service-properties update --account-name $storageAccount --enable-delete-retention true --delete-retention-days 30
    $storageConnString = az storage account show-connection-string -n $storageAccount -g $resourceGroup -o tsv

    # Create SQL Server and Database (Standard tier for production)
    Write-Host "Creating production Azure SQL Database: $sqlServerName"
    az sql server create -n $sqlServerName -g $resourceGroup -l $Location --admin-user $sqlAdminUser --admin-password $unsecureSqlAdminPassword
    az sql db create -s $sqlServerName -g $resourceGroup -n $sqlDbName --service-objective S2
    az sql server firewall-rule create -s $sqlServerName -g $resourceGroup -n AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
    # Configure automated backups for production
    az sql db update -s $sqlServerName -n $sqlDbName -g $resourceGroup --backup-storage-redundancy Zone
    $sqlConnString = az sql db show-connection-string -s $sqlServerName -n $sqlDbName -c ado.net | ForEach-Object { $_ -replace '<username>', $sqlAdminUser -replace '<password>', $unsecureSqlAdminPassword }

    # Create Key Vault with enhanced security
    Write-Host "Creating production Key Vault: $keyVaultName"
    az keyvault create -n $keyVaultName -g $resourceGroup -l $Location --enable-soft-delete true --enable-purge-protection true --enable-rbac-authorization false
    $kvCreateResult = az keyvault show -n $keyVaultName -g $resourceGroup --query name -o tsv 2>$null
    if (-not $kvCreateResult) {
        Write-Error "Failed to create or reach Key Vault '$keyVaultName'. Verify Azure CLI cloud, networking, and DNS resolution."
        exit 1
    }
    az keyvault secret set --vault-name $keyVaultName -n BlobStorage--ConnectionString --value $storageConnString
    az keyvault secret set --vault-name $keyVaultName -n Sql--ConnectionString --value $sqlConnString
    az keyvault secret set --vault-name $keyVaultName -n ApplicationInsights--InstrumentationKey --value $aiKey
    # Optional: set external auth provider secrets if integrating with external auth
    # az keyvault secret set --vault-name $keyVaultName -n ExternalAuth--BaseUrl --value "https://auth.school.edu"
    # az keyvault secret set --vault-name $keyVaultName -n ExternalAuth--ApiKey --value "production-api-key-placeholder"

    # Create App Service (Premium tier for production)
    Write-Host "Creating production App Service: $webAppName"
    az appservice plan create -n file-svc-production-plan -g $resourceGroup --sku P1v2 --is-linux false
    az webapp create -n $webAppName -g $resourceGroup -p file-svc-production-plan --runtime 'DOTNET|8.0'

    # Configure Managed Identity
    Write-Host "Configuring production Managed Identity..."
    az webapp identity assign -n $webAppName -g $resourceGroup
    $principalId = az webapp identity show -n $webAppName -g $resourceGroup --query principalId -o tsv
    az keyvault set-policy -n $keyVaultName -g $resourceGroup --object-id $principalId --secret-permissions get list
    $storageId = az storage account show -n $storageAccount -g $resourceGroup --query id -o tsv
    az role assignment create --assignee $principalId --role "Storage Blob Data Contributor" --scope $storageId

        # Configure App Settings
        Write-Host "Configuring production App Settings..."

        $kvBlobRef = '@Microsoft.KeyVault(VaultName=' + $keyVaultName + ';SecretName=BlobStorage--ConnectionString)'
        $kvSqlRef = '@Microsoft.KeyVault(VaultName=' + $keyVaultName + ';SecretName=Sql--ConnectionString)'
        $kvAiRef = '@Microsoft.KeyVault(VaultName=' + $keyVaultName + ';SecretName=ApplicationInsights--InstrumentationKey)'

        $appSettings = @(
            "ASPNETCORE_ENVIRONMENT=Production"
            "EnvironmentMode=Production"
            "BlobStorage__UseLocalStub=false"
            "BlobStorage__ConnectionString=$kvBlobRef"
            "BlobStorage__ContainerName=userfiles-prod"
            "Persistence__UseEf=true"
            "Persistence__UseSqlServer=true"
            "Sql__ConnectionString=$kvSqlRef"
            "ApplicationInsights__InstrumentationKey=$kvAiRef"
        )

        az webapp config appsettings set -n $webAppName -g $resourceGroup --settings $appSettings
        # Optional External Auth Key Vault references (uncomment if used)
        # Example: ExternalAuth__BaseUrl='@Microsoft.KeyVault(VaultName=<vault-name>;SecretName=ExternalAuth--BaseUrl)'
        # Example: ExternalAuth__ApiKey='@Microsoft.KeyVault(VaultName=<vault-name>;SecretName=ExternalAuth--ApiKey)'

    # Enhanced security settings for production
    Write-Host "Applying production security settings..."
    az webapp config set -n $webAppName -g $resourceGroup --https-only true --min-tls-version 1.2 --ftps-state Disabled

    Write-Host "✅ Production resources created successfully!" -ForegroundColor Green
    Write-Host "Production URL: https://$webAppName.azurewebsites.net" -ForegroundColor Green
}

if ($PromoteFromStaging) {
    Write-Host "🔄 Promoting staging build to production..." -ForegroundColor Yellow
    # Use the same build artifact from staging
    if (Test-Path "deploy-staging.zip") {
        Copy-Item "deploy-staging.zip" "deploy-production.zip"
        Write-Host "✅ Staging build promoted to production package" -ForegroundColor Green
    } else {
        Write-Error "Staging deployment package not found. Run staging deployment first."
        exit 1
    }
} elseif ($DeployApp) {
    Write-Host "🏗️ Creating fresh production build..." -ForegroundColor Yellow
    
    # Build and publish
    Write-Host "Building application for production..."
    dotnet publish src/FileService.Api/FileService.Api.csproj -c Release -o publish-production --verbosity quiet
    
    # Create migration bundle
    Write-Host "Creating migration bundle..."
    dotnet ef migrations bundle -p src/FileService.Infrastructure/FileService.Infrastructure.csproj -s src/FileService.Api/FileService.Api.csproj -o publish-production/efbundle.exe --verbose
    
    # Create deployment package
    Write-Host "Creating deployment package..."
    Set-Location publish-production
    Compress-Archive -Path * -DestinationPath ../deploy-production.zip -Force
    Set-Location ..
}

if ($DeployApp -or $PromoteFromStaging) {
    Write-Host "🚀 Deploying to production..." -ForegroundColor Yellow
    
    # Deploy to Azure
    az webapp deploy -g $resourceGroup -n $webAppName --src-path deploy-production.zip --type zip
    
    Write-Host "✅ Application deployed to production!" -ForegroundColor Green
}

if ($RunMigrations) {
    Write-Host "🗄️ Running database migrations on production..." -ForegroundColor Yellow
    
    # Get connection string from Key Vault and run migrations
    try {
        $connectionString = az keyvault secret show --vault-name $keyVaultName --name "Sql--ConnectionString" --query value -o tsv 2>$null
        if ($connectionString) {
            Write-Host "Running EF migrations on production database..."
            if ($PromoteFromStaging -and (Test-Path "publish-staging/efbundle.exe")) {
                ./publish-staging/efbundle.exe --connection $connectionString
            } elseif (Test-Path "publish-production/efbundle.exe") {
                ./publish-production/efbundle.exe --connection $connectionString
            } else {
                Write-Error "Migration bundle not found. Deploy application first."
                exit 1
            }
            Write-Host "✅ Production database migrations completed!" -ForegroundColor Green
        } else {
            Write-Error "Could not retrieve connection string from Key Vault '$keyVaultName'. DNS/networking or Key Vault permissions may be the issue."
            exit 1
        }
    } catch {
        Write-Error "Failed to run migrations: $($_.Exception.Message)"
    }
}

Write-Host ""
Write-Host "🎉 Production deployment completed!" -ForegroundColor Green
Write-Host "Production URL: https://filesvc-api-prod.azurewebsites.net" -ForegroundColor Cyan
Write-Host "Monitor logs: az webapp log tail -n filesvc-api-prod -g file-svc-production-rg" -ForegroundColor Gray

# Cleanup temp files
if (Test-Path "publish-production") { Remove-Item -Recurse -Force publish-production }
if (Test-Path "deploy-production.zip") { Remove-Item -Force deploy-production.zip }

# Clear sensitive variables from memory
if ($unsecureSqlAdminPassword) { $unsecureSqlAdminPassword = $null }
