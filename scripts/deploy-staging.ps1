#!/usr/bin/env pwsh
# deploy-staging.ps1 - Deploy File Service to Azure Staging Environment

param(
    [switch]$CreateResources,
    [switch]$DeployApp,
    [switch]$RunMigrations,
    [string]$SubscriptionId = "",
    [string]$Location = "chinaeast2",  # Azure China region
    [string]$ResourcePrefix = "filesvc-staging",
    [object]$SqlAdminPassword = $null
)

# Staging Environment Variables
$resourceGroup = "${ResourcePrefix}-rg"
$storageAccount = "${ResourcePrefix}stg$(Get-Random -Minimum 1000 -Maximum 9999)"
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

Write-Host "🧪 File Service - Staging Deployment" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan

if ($SubscriptionId) {
    Write-Host "Setting Azure subscription: $SubscriptionId"
    az account set --subscription $SubscriptionId
}

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
        Write-Host "Location appears to be an Azure China region ($LocationHint). Ensuring Azure CLI Cloud is set to AzureChinaCloud..." -ForegroundColor Yellow
        az cloud set --name AzureChinaCloud
        # Note: user still needs to run 'az login' for the selected cloud/subscription if not already authenticated.
    }
}

# Ensure Azure CLI is ready for this location
Assert-AzureCliReady -LocationHint $Location

# Ensure the resource group exists when the script needs to access Key Vault or other resources.
function New-ResourceGroupIfMissing {
    param(
        [string]$RgName,
        [string]$LocationHint
    )

    try {
        $exists = az group exists -n $RgName | ConvertFrom-Json
    } catch {
        # az group exists returns plain 'true'/'false' sometimes; handle fallback
        $exists = & az group exists -n $RgName 2>$null
    }

    if (-not $exists -or $exists -eq $false -or $exists -eq 'false') {
        if ($CreateResources) {
            # If we're already in create flow, creation will happen later; just warn
            Write-Host "Resource group '$RgName' does not exist yet; it will be created in the CreateResources step." -ForegroundColor Yellow
        } else {
            Write-Host "Resource group '$RgName' not found. Creating it now so dependent operations can continue..." -ForegroundColor Yellow
            az group create -n $RgName -l $LocationHint | Out-Null
            Write-Host "Resource group '$RgName' created." -ForegroundColor Green
        }
    } else {
        Write-Host "Resource group '$RgName' already exists." -ForegroundColor Gray
    }
}

# Ensure the resource group exists or create it now (if not in CreateResources flow)
New-ResourceGroupIfMissing -RgName $resourceGroup -LocationHint $Location

if ($CreateResources) {
    Write-Host "📦 Creating Azure resources for staging environment..." -ForegroundColor Yellow
    
    # Create staging resource group
    Write-Host "Creating resource group: $resourceGroup"
    az group create -n $resourceGroup -l $Location

    # Create Application Insights
    Write-Host "Creating Application Insights: $appInsightsName"
    az monitor app-insights component create -a $appInsightsName -g $resourceGroup -l $Location --application-type web
    $aiKey = az monitor app-insights component show -a $appInsightsName -g $resourceGroup --query instrumentationKey -o tsv

    # Create storage account
    Write-Host "Creating storage account: $storageAccount"
    az storage account create -n $storageAccount -g $resourceGroup -l $Location --sku Standard_LRS --kind StorageV2
    az storage container create --account-name $storageAccount -n userfiles-staging --auth-mode key --public-access off
    $storageConnString = az storage account show-connection-string -n $storageAccount -g $resourceGroup -o tsv

    # Create SQL Server and Database (Basic tier for staging)
    Write-Host "Creating Azure SQL Database: $sqlServerName"
    az sql server create -n $sqlServerName -g $resourceGroup -l $Location --admin-user $sqlAdminUser --admin-password $unsecureSqlAdminPassword
    az sql db create -s $sqlServerName -g $resourceGroup -n $sqlDbName --service-objective Basic
    az sql server firewall-rule create -s $sqlServerName -g $resourceGroup -n AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
    $sqlConnString = az sql db show-connection-string -s $sqlServerName -n $sqlDbName -c ado.net | ForEach-Object { $_ -replace '<username>', $sqlAdminUser -replace '<password>', $unsecureSqlAdminPassword }

    # Create Key Vault
    Write-Host "Creating Key Vault: $keyVaultName"
    az keyvault create -n $keyVaultName -g $resourceGroup -l $Location --enable-soft-delete true --enable-purge-protection true
    # Wait for the Key Vault to be ready and DNS to resolve (Azure can be eventually consistent)
    $maxAttempts = 12
    $attempt = 0
    $kvReady = $false
    while ($attempt -lt $maxAttempts -and -not $kvReady) {
        try {
            $attempt++
            Write-Host ("Checking Key Vault readiness (attempt {0}/{1})..." -f $attempt, $maxAttempts) -ForegroundColor Gray
            $kvUri = az keyvault show -n $keyVaultName -g $resourceGroup --query properties.vaultUri -o tsv 2>$null
            if ($kvUri) {
                # Try DNS resolution
                $hostName = ($kvUri -replace '^https?://','')
                try {
                    Resolve-DnsName $hostName -ErrorAction Stop | Out-Null
                    $kvReady = $true
                    break
                } catch {
                    Write-Host "Key Vault DNS not yet resolvable: $hostName" -ForegroundColor Yellow
                }
            } else {
                Write-Host "Key Vault not returned by az yet." -ForegroundColor Yellow
            }
        } catch {
            Write-Host "Key Vault show check failed: $($_.Exception.Message)" -ForegroundColor Yellow
        }
        Start-Sleep -Seconds 5
    }

    if (-not $kvReady) {
        Write-Error "Failed to confirm Key Vault '$keyVaultName' readiness after $maxAttempts attempts. Verify Azure CLI cloud, networking, and Key Vault permissions."
        exit 1
    }

    # Now set secrets
    az keyvault secret set --vault-name $keyVaultName -n BlobStorage--ConnectionString --value $storageConnString
    az keyvault secret set --vault-name $keyVaultName -n Sql--ConnectionString --value $sqlConnString
    az keyvault secret set --vault-name $keyVaultName -n ApplicationInsights--InstrumentationKey --value $aiKey
    # Optional: set external auth provider secrets if integrating with external auth
    # az keyvault secret set --vault-name $keyVaultName -n ExternalAuth--BaseUrl --value "https://test-auth.school.edu"
    # az keyvault secret set --vault-name $keyVaultName -n ExternalAuth--ApiKey --value "staging-api-key-placeholder"

    # Create App Service (Basic tier for staging)
    Write-Host "Creating App Service: $webAppName"
    az appservice plan create -n file-svc-staging-plan -g $resourceGroup --sku B1 --is-linux false
    az webapp create -n $webAppName -g $resourceGroup -p file-svc-staging-plan --runtime 'DOTNET|8.0'

    # Configure Managed Identity
    Write-Host "Configuring Managed Identity..."
    az webapp identity assign -n $webAppName -g $resourceGroup
    $principalId = az webapp identity show -n $webAppName -g $resourceGroup --query principalId -o tsv
    az keyvault set-policy -n $keyVaultName -g $resourceGroup --object-id $principalId --secret-permissions get list
    $storageId = az storage account show -n $storageAccount -g $resourceGroup --query id -o tsv
    az role assignment create --assignee $principalId --role "Storage Blob Data Contributor" --scope $storageId

    # Configure App Settings
    Write-Host "Configuring App Settings..."
    # Build Key Vault reference strings safely (no embedded Key Vault expressions in quoted comments)
    $kvBlobRef = '@Microsoft.KeyVault(VaultName=' + $keyVaultName + ';SecretName=BlobStorage--ConnectionString)'
    $kvSqlRef = '@Microsoft.KeyVault(VaultName=' + $keyVaultName + ';SecretName=Sql--ConnectionString)'
    $kvAiRef = '@Microsoft.KeyVault(VaultName=' + $keyVaultName + ';SecretName=ApplicationInsights--InstrumentationKey)'

            $appSettings = @(
                "ASPNETCORE_ENVIRONMENT=Staging"
                "EnvironmentMode=Staging"
                "BlobStorage__UseLocalStub=false"
                "BlobStorage__ConnectionString=$kvBlobRef"
                "BlobStorage__ContainerName=userfiles-staging"
                "Persistence__UseEf=true"
                "Persistence__UseSqlServer=true"
                "Sql__ConnectionString=$kvSqlRef"
                "ApplicationInsights__InstrumentationKey=$kvAiRef"
            )

            az webapp config appsettings set -n $webAppName -g $resourceGroup --settings $appSettings
    # Optional External Auth Key Vault References (uncomment and edit if used)
    # Example (replace <vault-name> and secret names):
    # ExternalAuth__BaseUrl='@Microsoft.KeyVault(VaultName=<vault-name>;SecretName=ExternalAuth--BaseUrl)'
    # ExternalAuth__ApiKey='@Microsoft.KeyVault(VaultName=<vault-name>;SecretName=ExternalAuth--ApiKey)'

    # Security Settings
    Write-Host "Applying security settings..."
    az webapp config set -n $webAppName -g $resourceGroup --https-only true --min-tls-version 1.2

    Write-Host "✅ Staging resources created successfully!" -ForegroundColor Green
    Write-Host "Staging URL: https://$webAppName.azurewebsites.net" -ForegroundColor Green
}

if ($DeployApp) {
    Write-Host "🚀 Deploying application to staging..." -ForegroundColor Yellow
    
    # Build and publish
    Write-Host "Building application..."
    dotnet publish src/FileService.Api/FileService.Api.csproj -c Release -o publish-staging --verbosity quiet
    
    # Create migration bundle
    Write-Host "Creating migration bundle..."
    dotnet ef migrations bundle -p src/FileService.Infrastructure/FileService.Infrastructure.csproj -s src/FileService.Api/FileService.Api.csproj -o publish-staging/efbundle.exe --verbose
    
    # Create deployment package
    Write-Host "Creating deployment package..."
    Set-Location publish-staging
    Compress-Archive -Path * -DestinationPath ../deploy-staging.zip -Force
    Set-Location ..
    
    # Deploy to Azure
    Write-Host "Deploying to Azure App Service..."
    az webapp deploy -g $resourceGroup -n $webAppName --src-path deploy-staging.zip --type zip
    
    Write-Host "✅ Application deployed to staging!" -ForegroundColor Green
}

if ($RunMigrations) {
    Write-Host "🗄️ Running database migrations on staging..." -ForegroundColor Yellow
    
    # Get connection string from Key Vault and run migrations
    try {
        $connectionString = az keyvault secret show --vault-name $keyVaultName --name "Sql--ConnectionString" --query value -o tsv 2>$null
        if ($connectionString) {
            Write-Host "Running EF migrations..."
            ./publish-staging/efbundle.exe --connection $connectionString
            Write-Host "✅ Database migrations completed!" -ForegroundColor Green
        } else {
            Write-Error "Could not retrieve connection string from Key Vault '$keyVaultName'. DNS/networking or Key Vault permissions may be the issue."
            exit 1
        }
    } catch {
        Write-Error "Failed to run migrations: $($_.Exception.Message)"
    }
}

Write-Host ""
Write-Host "🎉 Staging deployment completed!" -ForegroundColor Green
Write-Host "Staging URL: https://filesvc-api-staging.azurewebsites.net/swagger" -ForegroundColor Cyan
Write-Host "Monitor logs: az webapp log tail -n filesvc-api-staging -g file-svc-staging-rg" -ForegroundColor Gray

# Cleanup temp files
if (Test-Path "publish-staging") { Remove-Item -Recurse -Force publish-staging }
if (Test-Path "deploy-staging.zip") { Remove-Item -Force deploy-staging.zip }

# Clear sensitive variables from memory
if ($unsecureSqlAdminPassword) { $unsecureSqlAdminPassword = $null }
