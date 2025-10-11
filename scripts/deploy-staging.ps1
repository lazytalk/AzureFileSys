#!/usr/bin/env pwsh
# deploy-staging.ps1 - Deploy File Service to Azure Staging Environment

param(
    [switch]$CreateResources,
    [switch]$DeployApp,
    [switch]$RunMigrations,
    [string]$SubscriptionId = "",
    [string]$Location = "chinaeast2"  # Azure China region
)

# Staging Environment Variables
$env = "staging"
$resourceGroup = "file-svc-staging-rg"
$storageAccount = "filesvcstg$(Get-Random -Minimum 1000 -Maximum 9999)"
$webAppName = "filesvc-api-staging"
$keyVaultName = "filesvc-kv-staging"
$appInsightsName = "filesvc-ai-staging"
$sqlServerName = "filesvc-sql-staging"
$sqlDbName = "file-service-db"
$sqlAdminUser = "fsadmin"
$sqlAdminPassword = "YourSecurePassword123!" # CHANGE THIS TO A SECURE PASSWORD!

Write-Host "🧪 File Service - Staging Deployment" -ForegroundColor Cyan
Write-Host "===================================" -ForegroundColor Cyan

if ($SubscriptionId) {
    Write-Host "Setting Azure subscription: $SubscriptionId"
    az account set --subscription $SubscriptionId
}

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
    az sql server create -n $sqlServerName -g $resourceGroup -l $Location --admin-user $sqlAdminUser --admin-password $sqlAdminPassword
    az sql db create -s $sqlServerName -g $resourceGroup -n $sqlDbName --service-objective Basic
    az sql server firewall-rule create -s $sqlServerName -g $resourceGroup -n AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
    $sqlConnString = az sql db show-connection-string -s $sqlServerName -n $sqlDbName -c ado.net | ForEach-Object { $_ -replace '<username>', $sqlAdminUser -replace '<password>', $sqlAdminPassword }

    # Create Key Vault
    Write-Host "Creating Key Vault: $keyVaultName"
    az keyvault create -n $keyVaultName -g $resourceGroup -l $Location --enable-soft-delete true --enable-purge-protection true
    az keyvault secret set --vault-name $keyVaultName -n BlobStorage--ConnectionString --value $storageConnString
    az keyvault secret set --vault-name $keyVaultName -n Sql--ConnectionString --value $sqlConnString
    az keyvault secret set --vault-name $keyVaultName -n ApplicationInsights--InstrumentationKey --value $aiKey
    az keyvault secret set --vault-name $keyVaultName -n PowerSchool--BaseUrl --value "https://test-powerschool.school.edu"
    az keyvault secret set --vault-name $keyVaultName -n PowerSchool--ApiKey --value "staging-api-key-placeholder"

    # Create App Service (Basic tier for staging)
    Write-Host "Creating App Service: $webAppName"
    az appservice plan create -n file-svc-staging-plan -g $resourceGroup --sku B1 --is-linux false
    az webapp create -n $webAppName -g $resourceGroup -p file-svc-staging-plan --runtime "DOTNET|8.0"

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
      ASPNETCORE_ENVIRONMENT=Staging `
      EnvironmentMode=Staging `
      BlobStorage__UseLocalStub=false `
      "BlobStorage__ConnectionString=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=BlobStorage--ConnectionString)" `
      BlobStorage__ContainerName=userfiles-staging `
      Persistence__UseEf=true `
      Persistence__UseSqlServer=true `
      "Sql__ConnectionString=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=Sql--ConnectionString)" `
      "ApplicationInsights__InstrumentationKey=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=ApplicationInsights--InstrumentationKey)" `
      "PowerSchool__BaseUrl=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=PowerSchool--BaseUrl)" `
      "PowerSchool__ApiKey=@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=PowerSchool--ApiKey)"

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
        $connectionString = az keyvault secret show --vault-name $keyVaultName --name "Sql--ConnectionString" --query value -o tsv
        if ($connectionString) {
            Write-Host "Running EF migrations..."
            ./publish-staging/efbundle.exe --connection $connectionString
            Write-Host "✅ Database migrations completed!" -ForegroundColor Green
        } else {
            Write-Warning "Could not retrieve connection string from Key Vault"
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
