#!/usr/bin/env pwsh
# deploy.ps1 - Unified deployment script for staging and production environments
#
# This script handles:
# - Azure resource creation (Storage, SQL, Key Vault, App Service, etc.)
# - Application deployment
# - Database migrations
# - Custom domain configuration with DNS validation
# - SSL certificate creation and binding
#
# Usage Examples:
# 1. Create all resources:
#    .\deploy.ps1 -Environment Staging -CreateResources
#
# 2. Deploy application:
#    .\deploy.ps1 -Environment Staging -DeployApp
#
# 3. Run database migrations:
#    .\deploy.ps1 -Environment Staging -RunMigrations
#
# 4. Configure custom domain with SSL (uses domain from deploy-settings.ps1):
#    .\deploy.ps1 -Environment Staging -ConfigureCustomDomain
#
# 5. Configure custom domain with SSL (override with specific domain):
#    .\deploy.ps1 -Environment Staging -ConfigureCustomDomain -CustomDomain "custom.example.com"
#
# 6. Full deployment with custom domain:
#    .\deploy.ps1 -Environment Production -CreateResources -DeployApp -RunMigrations -ConfigureCustomDomain
#
# 7. Promote staging to production:
#    .\deploy.ps1 -Environment Production -PromoteFromStaging

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Staging", "Production")]
    [string]$Environment,
    
    [switch]$CreateResources,
    [switch]$DeployApp,
    [switch]$RunMigrations,
    [switch]$PromoteFromStaging,
    [switch]$ConfigureCustomDomain,
    [string]$CustomDomain = "",  # e.g., "filesvc-stg-app.kaiweneducation.com"
    [string]$SubscriptionId = "",
    [string]$Location = "",
    [string]$SqlAdminPassword = ""
)

Set-Variable -Name ErrorActionPreference -Value 'Stop' -Scope Script

# Set UTF-8 encoding for better compatibility (must be after param)
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

# ============================================================================
# ENVIRONMENT CONFIGURATION
# ============================================================================

$config = @{
    Staging = @{
        Color = "Cyan"
        Emoji = "🧪"
        IsProduction = $false
    }
    Production = @{
        Color = "Magenta"
        Emoji = "🚀"
        IsProduction = $true
    }
}

$env = $config[$Environment]

# ============================================================================
# SECTION 1: CHECK AZURE CLI PREREQUISITES
# ============================================================================

Write-Host "Checking Azure CLI prerequisites..." -ForegroundColor $env.Color

# Check if Azure CLI is installed
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Error "Azure CLI is not installed. Please install from https://aka.ms/azure-cli"
    exit 1
}

# Set Azure China Cloud
Write-Host "Setting Azure China Cloud..." -ForegroundColor $env.Color

# Unset environment variable that might override the cloud setting
if (Test-Path Env:\AZURE_CLOUD_NAME) { Remove-Item Env:\AZURE_CLOUD_NAME }

az cloud set --name AzureChinaCloud
$currentCloud = az cloud show --query name -o tsv

if ($currentCloud -eq "AzureChinaCloud") {
    Write-Host "✓ Azure China Cloud configured" -ForegroundColor Green
} else {
    Write-Error "Failed to set Azure China Cloud. Current cloud: $currentCloud"
    exit 1
}

# Check if logged in to Azure
try {
    $accountInfo = az account show -o json | ConvertFrom-Json
    Write-Host "✓ Authenticated as: $($accountInfo.user.name)" -ForegroundColor Green
    Write-Host "✓ Current subscription: $($accountInfo.name) ($($accountInfo.id))" -ForegroundColor Green
} catch {
    Write-Host "Not logged in to Azure CLI. Initiating login..." -ForegroundColor Yellow
    az login
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Azure login failed"
        exit 1
    }
    
    # Verify login succeeded
    $accountInfo = az account show -o json | ConvertFrom-Json
    Write-Host "✓ Authenticated as: $($accountInfo.user.name)" -ForegroundColor Green
}

Write-Host "" # Empty line for readability

# ============================================================================
# SECTION 2: INITIALIZE DEPLOYMENT CONTEXT
# ============================================================================

# Load unified configuration (resources + app settings)
$config = & (Join-Path $PSScriptRoot "deploy-settings.ps1") -Environment $Environment -SqlAdminPassword $SqlAdminPassword
$resources = $config.Resources
$appSettings = $config.AppSettings

# Apply Location override if provided via command line
if (-not [string]::IsNullOrWhiteSpace($Location)) {
    $resources["Location"] = $Location
}

# Set subscription if provided
if (-not [string]::IsNullOrWhiteSpace($SubscriptionId)) {
    Write-Host "Setting Azure subscription: $SubscriptionId"
    az account set --subscription $SubscriptionId
}

# ============================================================================
# SECTION 3: PRINT DEPLOYMENT BANNER
# ============================================================================

Write-Host "$($env.Emoji) File Service - $Environment Deployment" -ForegroundColor $env.Color
Write-Host ("=" * (20 + $Environment.Length)) -ForegroundColor $env.Color

# Helper to check if a resource exists to avoid redundant creation calls
function Test-ResourceExists {
    param($Command)
    $ErrorActionPreference = "SilentlyContinue"
    try {
        Invoke-Expression $Command | Out-Null
        return ($LASTEXITCODE -eq 0)
    } catch {
        return $false
    }
    finally {
        $ErrorActionPreference = "Stop"
    }
}

# ============================================================================
# SECTION 4: CREATE AZURE RESOURCES (if requested)
# ============================================================================

if ($CreateResources) {
    Write-Host "📦 Ensuring Azure resources for $Environment environment..." -ForegroundColor Yellow
    
    $resourceGroup = $resources["ResourceGroup"]
    $storageAccount = $resources["StorageAccount"]
    $webAppName = $resources["WebAppName"]
    $keyVaultName = $resources["KeyVaultName"]
    $appInsightsName = $resources["AppInsightsName"]
    $sqlServerName = $resources["SqlServerName"]
    $sqlDbName = $resources["SqlDbName"]
    $sqlAdminUser = $resources["SqlAdminUser"]
    $sqlAdminPassword = $resources["SqlAdminPassword"]
    $sqlTier = $resources["SqlTier"]
    $appServiceSku = $resources["AppServiceSku"]
    $appServicePlanName = $resources["AppServicePlanName"]
    $envLabel = $resources["EnvLabel"]
    
    # Create resource group
    if (Test-ResourceExists "az group show -n $resourceGroup") {
        Write-Host "Using existing resource group: $resourceGroup" -ForegroundColor Gray
    } else {
        Write-Host "Creating resource group: $resourceGroup"
        az group create -n $resourceGroup -l $resources["Location"]
    }

    # Create Application Insights
    if (Test-ResourceExists "az monitor app-insights component show -a $appInsightsName -g $resourceGroup") {
        Write-Host "Using existing Application Insights: $appInsightsName" -ForegroundColor Gray
    } else {
        Write-Host "Creating Application Insights: $appInsightsName"
        az monitor app-insights component create -a $appInsightsName -g $resourceGroup -l $resources["Location"] --application-type web
    }
    $aiKey = az monitor app-insights component show -a $appInsightsName -g $resourceGroup --query instrumentationKey -o tsv
    
    # Create storage account
    if (Test-ResourceExists "az storage account show -n $storageAccount -g $resourceGroup") {
        Write-Host "Using existing storage account: $storageAccount" -ForegroundColor Gray
    } else {
        Write-Host "Creating storage account: $storageAccount"
        if ($env.IsProduction) {
            az storage account create -n $storageAccount -g $resourceGroup -l $resources["Location"] --sku Standard_GRS --kind StorageV2 --enable-versioning true
            az storage container create --account-name $storageAccount -n "userfiles-$envLabel" --auth-mode key --public-access off
            az storage account blob-service-properties update --account-name $storageAccount --enable-delete-retention true --delete-retention-days 30
        } else {
            az storage account create -n $storageAccount -g $resourceGroup -l $resources["Location"] --sku Standard_LRS --kind StorageV2
            az storage container create --account-name $storageAccount -n "userfiles-$envLabel" --auth-mode key --public-access off
        }
    }
    $storageConnString = az storage account show-connection-string -n $storageAccount -g $resourceGroup -o tsv
    
    # Create SQL Server and Database
    if (Test-ResourceExists "az sql server show -n $sqlServerName -g $resourceGroup") {
        Write-Host "Using existing SQL Server: $sqlServerName" -ForegroundColor Gray
    } else {
        Write-Host "Creating Azure SQL Database: $sqlServerName"
        az sql server create -n $sqlServerName -g $resourceGroup -l $resources["Location"] --admin-user $sqlAdminUser --admin-password $sqlAdminPassword
        az sql server firewall-rule create -s $sqlServerName -g $resourceGroup -n AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
    }
    
    if (Test-ResourceExists "az sql db show -s $sqlServerName -g $resourceGroup -n $sqlDbName") {
        Write-Host "Using existing SQL Database: $sqlDbName" -ForegroundColor Gray
    } else {
        Write-Host "Creating SQL Database: $sqlDbName"
        az sql db create -s $sqlServerName -g $resourceGroup -n $sqlDbName --service-objective $sqlTier
        
        if ($env.IsProduction) {
            az sql db update -s $sqlServerName -n $sqlDbName -g $resourceGroup --backup-storage-redundancy Zone
        }
    }
    
    $sqlConnString = az sql db show-connection-string -s $sqlServerName -n $sqlDbName -c ado.net | ForEach-Object { $_ -replace '<username>', $sqlAdminUser -replace '<password>', $sqlAdminPassword }
    
    # Create Key Vault
    if (Test-ResourceExists "az keyvault show -n $keyVaultName -g $resourceGroup") {
        Write-Host "Using existing Key Vault: $keyVaultName" -ForegroundColor Gray
    } else {
        Write-Host "Creating Key Vault: $keyVaultName"
        # Note: Purge protection prevented cleanup during development. Removed for non-prod scenarios or handled carefully.
        if ($env.IsProduction) {
            az keyvault create -n $keyVaultName -g $resourceGroup -l $resources["Location"] --enable-purge-protection true
        } else {
            az keyvault create -n $keyVaultName -g $resourceGroup -l $resources["Location"]
        }
    }
    
    # FIX: Ensure current user has permissions (Crucial for RBAC enabled Key Vaults)
    # We need this BEFORE trying to set secrets
    Write-Host "Checking Key Vault permissions..." -ForegroundColor Gray
    $kvId = az keyvault show -n $keyVaultName -g $resourceGroup --query id -o tsv
    $isRbac = az keyvault show -n $keyVaultName -g $resourceGroup --query properties.enableRbacAuthorization -o tsv
    
    if ($isRbac -eq "true") {
        Write-Host "Key Vault is using RBAC. Assigning permissions to current user..." -ForegroundColor Cyan
        $currentUserId = az ad signed-in-user show --query id -o tsv
        
        # Assign Key Vault Administrator to current user to allow secret/cert management
        # We catch error in case assignment already exists (though 'create' handles it usually)
        try {
            az role assignment create --assignee $currentUserId --role "Key Vault Administrator" --scope $kvId --output none 2>&1 | Out-Null
            Write-Host "✓ Key Vault Administrator role assigned to current user" -ForegroundColor Green
        } catch {
            Write-Host "⚠ Note: Could not assign role (might already exist or permission denied): $_" -ForegroundColor Yellow
        }
        
        # Allow time for propagation if we suspect it was just created
        Write-Host "Waiting 15s for RBAC propagation..." -ForegroundColor Gray
        Start-Sleep -Seconds 15
    }

    # Always update secrets in case they changed
    Write-Host "Updating Key Vault secrets..." -ForegroundColor Gray
    az keyvault secret set --vault-name $keyVaultName -n "BlobStorage--ConnectionString" --value $storageConnString > $null
    az keyvault secret set --vault-name $keyVaultName -n "Sql--ConnectionString" --value $sqlConnString > $null
    az keyvault secret set --vault-name $keyVaultName -n "ApplicationInsights--InstrumentationKey" --value $aiKey > $null
    az keyvault secret set --vault-name $keyVaultName -n "PowerSchool--BaseUrl" --value $resources["PowerSchoolBaseUrl"] > $null
    az keyvault secret set --vault-name $keyVaultName -n "PowerSchool--ApiKey" --value "$envLabel-api-key-placeholder" > $null
    
    # Create App Service
    if (Test-ResourceExists "az appservice plan show -n $appServicePlanName -g $resourceGroup") {
        Write-Host "Using existing App Service Plan: $appServicePlanName" -ForegroundColor Gray
    } else {
        Write-Host "Creating App Service Plan: $appServicePlanName"
        az appservice plan create -n $appServicePlanName -g $resourceGroup --sku $appServiceSku
    }
    
    if (Test-ResourceExists "az webapp show -n $webAppName -g $resourceGroup") {
        Write-Host "Using existing App Service: $webAppName" -ForegroundColor Gray
    } else {
        Write-Host "Creating App Service: $webAppName"
        # Using string explicit quoting to avoid powershell pipe interpretation
        az webapp create -n $webAppName -g $resourceGroup -p $appServicePlanName --runtime "dotnet:9"
    }
    
    # Configure Managed Identity (Idempotent, safe to rerun)
    Write-Host "Configuring Managed Identity..."
    az webapp identity assign -n $webAppName -g $resourceGroup
    $principalId = az webapp identity show -n $webAppName -g $resourceGroup --query principalId -o tsv
    
    # Assign RBAC role for Key Vault
    $kvId = az keyvault show -n $keyVaultName -g $resourceGroup --query id -o tsv
    # Secrets User for reading secrets (connection strings)
    az role assignment create --assignee $principalId --role "Key Vault Secrets User" --scope $kvId
    # Certificates Officer (or User) for importing certificates
    az role assignment create --assignee $principalId --role "Key Vault Certificates Officer" --scope $kvId
    
    $storageId = az storage account show -n $storageAccount -g $resourceGroup --query id -o tsv
    az role assignment create --assignee $principalId --role "Storage Blob Data Contributor" --scope $storageId
    
    # Configure App Settings
    Write-Host "Configuring App Settings..."
    
    # Use JSON format to avoid shell quoting issues with KeyVault references
    $finalSettings = $appSettings.Clone()
    $finalSettings["BlobStorage__ConnectionString"] = "@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=BlobStorage--ConnectionString)"
    $finalSettings["Sql__ConnectionString"] = "@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=Sql--ConnectionString)"
    $finalSettings["ApplicationInsights__InstrumentationKey"] = "@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=ApplicationInsights--InstrumentationKey)"
    $finalSettings["PowerSchool__BaseUrl"] = "@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=PowerSchool--BaseUrl)"
    $finalSettings["PowerSchool__ApiKey"] = "@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=PowerSchool--ApiKey)"

    $json = $finalSettings | ConvertTo-Json -Compress
    # Escape double quotes for shell compatibility (Windows argument passing)
    $jsonArg = $json.Replace('"', '\"')
    
    az webapp config appsettings set -n $webAppName -g $resourceGroup --settings "$jsonArg"
    
    # Security Settings
    Write-Host "Applying security settings..."
    if ($env.IsProduction) {
        az webapp config set -n $webAppName -g $resourceGroup --min-tls-version 1.2 --ftps-state Disabled
    } else {
        az webapp config set -n $webAppName -g $resourceGroup --min-tls-version 1.2
    }
    
    # Get the actual default host name (handles Azure China .chinacloudsites.cn vs Global .azurewebsites.net)
    $hostName = az webapp show -n $webAppName -g $resourceGroup --query defaultHostName -o tsv
    
    Write-Host "✅ $Environment resources created successfully!" -ForegroundColor Green
    Write-Host "$Environment URL: https://$hostName" -ForegroundColor Green
}

# ============================================================================
# SECTION 5: DEPLOY APPLICATION (if requested)
# ============================================================================

if ($Environment -eq "Production" -and $PromoteFromStaging) {
    Write-Host "🔄 Promoting staging build to production..." -ForegroundColor Yellow
    if (Test-Path "deploy-staging.zip") {
        Copy-Item "deploy-staging.zip" "deploy-production.zip"
        Write-Host "✅ Staging build promoted to production package" -ForegroundColor Green
        
        # Deploy without rebuilding
        Write-Host "Deploying to Azure App Service..."
        az webapp deploy -g $resources["ResourceGroup"] -n $resources["WebAppName"] --src-path "deploy-production.zip" --type zip
    } else {
        Write-Error "Staging deployment package not found. Run staging deployment first."
        exit 1
    }
} elseif ($DeployApp) {
    Write-Host "🚀 Deploying application to $Environment..." -ForegroundColor Yellow
    
    $environmentLabel = $Environment.ToLower()
    $publishDir = "publish-$environmentLabel"
    $deployZip = "deploy-$environmentLabel.zip"
    
    # Build and publish
    Write-Host "Building application..."
    dotnet publish src/FileService.Api/FileService.Api.csproj -c Release -o $publishDir --verbosity quiet
    
    # Run EF Migrations
    Write-Host "Creating migration bundle..."
    $env:Persistence__ForceEf="true"
    dotnet ef migrations bundle -p src/FileService.Infrastructure/FileService.Infrastructure.csproj -s src/FileService.Api/FileService.Api.csproj -o "$publishDir/efbundle.exe" --verbose
    $env:Persistence__ForceEf="false"
    
    # Create deployment package
    Write-Host "Creating deployment package..."
    Set-Location $publishDir
    Compress-Archive -Path * -DestinationPath "../$deployZip" -Force
    Set-Location ..
    
    # Deploy to Azure
    Write-Host "Deploying to Azure App Service..."
    az webapp deploy -g $resources["ResourceGroup"] -n $resources["WebAppName"] --src-path $deployZip --type zip
    
    Write-Host "✅ Application deployed to $Environment!" -ForegroundColor Green
}

# ============================================================================
# SECTION 6: CONFIGURE CUSTOM DOMAIN & SSL (if requested)
# ============================================================================

if ($ConfigureCustomDomain) {
    # Use custom domain from settings if not provided via parameter
    if ([string]::IsNullOrWhiteSpace($CustomDomain)) {
        $CustomDomain = $resources["CustomDomain"]
    }
    
    if ([string]::IsNullOrWhiteSpace($CustomDomain)) {
        Write-Host "⚠ No custom domain configured for $Environment environment" -ForegroundColor Yellow
        Write-Host "Either add CustomDomain to deploy-settings.ps1 or use -CustomDomain parameter" -ForegroundColor Yellow
    } else {
        Write-Host "🌐 Configuring custom domain and SSL for $Environment..." -ForegroundColor Yellow
        
        $webAppName = $resources["WebAppName"]
        $resourceGroup = $resources["ResourceGroup"]
    
    Write-Host ""
    Write-Host "============================================" -ForegroundColor Cyan
    Write-Host "Custom Domain & SSL Configuration" -ForegroundColor Cyan
    Write-Host "============================================" -ForegroundColor Cyan
    Write-Host "Web App: $webAppName" -ForegroundColor Yellow
    Write-Host "Resource Group: $resourceGroup" -ForegroundColor Yellow
    Write-Host "Custom Domain: $CustomDomain" -ForegroundColor Yellow
    Write-Host ""
    
    # Step 1: Get App Service information for DNS configuration
    Write-Host "Step 1: Getting App Service information..." -ForegroundColor Cyan
    $defaultHostname = az webapp show --name $webAppName --resource-group $resourceGroup --query "defaultHostName" -o tsv
    $verificationId = az webapp show --name $webAppName --resource-group $resourceGroup --query "customDomainVerificationId" -o tsv
    
    Write-Host "✓ Default hostname: $defaultHostname" -ForegroundColor Green
    Write-Host "✓ Verification ID: $verificationId" -ForegroundColor Green
    
    # Step 2: Check if custom domain is already configured
    Write-Host ""
    Write-Host "Step 2: Checking if custom domain is already configured..." -ForegroundColor Cyan
    $existingDomains = az webapp config hostname list --webapp-name $webAppName --resource-group $resourceGroup -o json | ConvertFrom-Json
    $domainExists = $existingDomains | Where-Object { $_.name -eq $CustomDomain }
    
    if ($domainExists) {
        Write-Host "✓ Custom domain $CustomDomain is already configured" -ForegroundColor Green
    } else {
        Write-Host "Custom domain not configured yet" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "============================================" -ForegroundColor Yellow
        Write-Host "DNS Configuration Required:" -ForegroundColor Yellow
        Write-Host "============================================" -ForegroundColor Yellow
        Write-Host "The following DNS records are required:" -ForegroundColor White
        Write-Host ""
        Write-Host "1. CNAME Record:" -ForegroundColor Cyan
        Write-Host "   Name:  $CustomDomain" -ForegroundColor White
        Write-Host "   Type:  CNAME" -ForegroundColor White
        Write-Host "   Value: $defaultHostname" -ForegroundColor White
        Write-Host ""
        Write-Host "2. TXT Record (for verification):" -ForegroundColor Cyan
        Write-Host "   Name:  asuid.$CustomDomain" -ForegroundColor White
        Write-Host "   Type:  TXT" -ForegroundColor White
        Write-Host "   Value: $verificationId" -ForegroundColor White
        Write-Host ""
        Write-Host "============================================" -ForegroundColor Yellow
        
        # Automatic DNS validation
        Write-Host ""
        Write-Host "Step 3: Validating DNS configuration..." -ForegroundColor Cyan
        $dnsValid = $true
        $maxRetries = 3
        $retryDelay = 5
        
        # Check CNAME record
        Write-Host "Checking CNAME record for $CustomDomain..." -ForegroundColor Gray
        for ($i = 1; $i -le $maxRetries; $i++) {
            try {
                $cnameResult = Resolve-DnsName -Name $CustomDomain -Type CNAME -ErrorAction Stop
                if ($cnameResult.NameHost -like "*$defaultHostname*" -or $cnameResult.NameHost -like "*.azurewebsites.net" -or $cnameResult.NameHost -like "*.chinacloudsites.cn") {
                    Write-Host "✓ CNAME record found: $($cnameResult.NameHost)" -ForegroundColor Green
                    break
                } else {
                    Write-Host "⚠ CNAME points to: $($cnameResult.NameHost) (expected: $defaultHostname)" -ForegroundColor Yellow
                    $dnsValid = $false
                }
            } catch {
                if ($i -lt $maxRetries) {
                    Write-Host "CNAME not found, retrying in $retryDelay seconds... (attempt $i/$maxRetries)" -ForegroundColor Yellow
                    Start-Sleep -Seconds $retryDelay
                } else {
                    Write-Host "✗ CNAME record not found or not propagated yet" -ForegroundColor Red
                    $dnsValid = $false
                }
            }
        }
        
        # Check TXT record
        Write-Host "Checking TXT record for asuid.$CustomDomain..." -ForegroundColor Gray
        for ($i = 1; $i -le $maxRetries; $i++) {
            try {
                $txtResult = Resolve-DnsName -Name "asuid.$CustomDomain" -Type TXT -ErrorAction Stop
                $txtRecord = $txtResult | Where-Object { $_.Strings -contains $verificationId }
                if ($txtRecord) {
                    Write-Host "✓ TXT verification record found" -ForegroundColor Green
                    break
                } else {
                    Write-Host "⚠ TXT record found but doesn't match verification ID" -ForegroundColor Yellow
                    $dnsValid = $false
                }
            } catch {
                if ($i -lt $maxRetries) {
                    Write-Host "TXT record not found, retrying in $retryDelay seconds... (attempt $i/$maxRetries)" -ForegroundColor Yellow
                    Start-Sleep -Seconds $retryDelay
                } else {
                    Write-Host "✗ TXT verification record not found or not propagated yet" -ForegroundColor Red
                    $dnsValid = $false
                }
            }
        }
        
        if (-not $dnsValid) {
            Write-Host ""
            Write-Host "DNS records are not properly configured or not propagated yet." -ForegroundColor Yellow
            Write-Host ""
            Write-Host "Options:" -ForegroundColor Cyan
            Write-Host "1. Wait for DNS propagation (can take 5-60 minutes) and run the script again" -ForegroundColor White
            Write-Host "2. Proceed anyway and configure manually in Azure Portal" -ForegroundColor White
            Write-Host "3. Exit and configure DNS records now" -ForegroundColor White
            Write-Host ""
            
            $choice = Read-Host "Enter your choice (1/2/3)"
            switch ($choice) {
                "1" {
                    Write-Host "Please wait for DNS propagation and run the script again." -ForegroundColor Yellow
                    exit 0
                }
                "2" {
                    Write-Host "Proceeding with manual configuration..." -ForegroundColor Yellow
                }
                "3" {
                    Write-Host "Please configure DNS records and run the script again." -ForegroundColor Yellow
                    exit 0
                }
                default {
                    Write-Host "Invalid choice. Exiting." -ForegroundColor Red
                    exit 1
                }
            }
        } else {
            Write-Host ""
            Write-Host "✓ DNS configuration validated successfully!" -ForegroundColor Green
        }
        
        Write-Host ""
        Write-Host "Step 4: Adding custom domain..." -ForegroundColor Cyan
        $validation = az webapp config hostname add --webapp-name $webAppName --resource-group $resourceGroup --hostname $CustomDomain 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Error adding custom domain: $validation" -ForegroundColor Red
            Write-Host ""
            Write-Host "Common issues:" -ForegroundColor Yellow
            Write-Host "1. DNS records not yet propagated (wait 5-10 minutes)" -ForegroundColor White
            Write-Host "2. TXT verification record missing or incorrect" -ForegroundColor White
            Write-Host "3. CNAME record not pointing to $defaultHostname" -ForegroundColor White
            Write-Host ""
            Write-Host "To manually check DNS propagation:" -ForegroundColor Cyan
            Write-Host "  Resolve-DnsName -Name $CustomDomain -Type CNAME" -ForegroundColor White
            Write-Host "  Resolve-DnsName -Name asuid.$CustomDomain -Type TXT" -ForegroundColor White
            exit 1
        }
        Write-Host "✓ Custom domain added successfully!" -ForegroundColor Green
    }
    
    # Step 3: Check if SSL certificate exists for the domain
    Write-Host ""
    Write-Host "Step 5: Checking SSL certificate status..." -ForegroundColor Cyan
    $certificates = az webapp config ssl list --resource-group $resourceGroup -o json | ConvertFrom-Json
    $existingCert = $certificates | Where-Object { $_.hostNames -contains $CustomDomain }
    
    if ($existingCert) {
        Write-Host "✓ SSL certificate already exists for $CustomDomain" -ForegroundColor Green
        Write-Host "  Thumbprint: $($existingCert.thumbprint)" -ForegroundColor Gray
        
        # Check if it's bound
        $sslBindings = az webapp config ssl list --resource-group $resourceGroup -o json | ConvertFrom-Json
        $boundCert = $sslBindings | Where-Object { $_.hostNames -contains $CustomDomain }
        
        if ($boundCert) {
            Write-Host "✓ SSL certificate is already bound to the custom domain" -ForegroundColor Green
        } else {
            Write-Host "SSL certificate exists but not bound, binding now..." -ForegroundColor Yellow
            az webapp config ssl bind `
                --name $webAppName `
                --resource-group $resourceGroup `
                --certificate-thumbprint $($existingCert.thumbprint) `
                --ssl-type SNI `
                2>&1 | Out-Null
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✓ SSL certificate bound successfully!" -ForegroundColor Green
            } else {
                Write-Host "Warning: Failed to bind existing certificate" -ForegroundColor Yellow
            }
        }
    } else {
        # Step 4: Create managed SSL certificate
        Write-Host "No SSL certificate found, creating managed certificate..." -ForegroundColor Yellow
        Write-Host "This may take 2-3 minutes..." -ForegroundColor Yellow
        
        # Suppress warning messages from Azure CLI about preview features
        $oldErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        
        $result = az webapp config ssl create `
            --name $webAppName `
            --resource-group $resourceGroup `
            --hostname $CustomDomain `
            2>&1 | Where-Object { $_ -notmatch 'WARNING:' }
        
        $ErrorActionPreference = $oldErrorActionPreference
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Error creating managed SSL certificate:" -ForegroundColor Red
            Write-Host $result -ForegroundColor Red
            
            # Use smart detection logic to see if it's a validation error vs support error
            if ($result -match "Hostname not found" -or $result -match "DNS" -or $result -match "validation") {
                 Write-Host ""
                 Write-Host "❌ Validation Failed: The managed certificate could not be created because validation failed." -ForegroundColor Red
                 Write-Host "   This usually means DNS records haven't propagated yet." -ForegroundColor Yellow
                 Write-Host "   Managed SSL IS supported in this region, but requires correct DNS setup." -ForegroundColor Yellow
                 Write-Host ""
                 Write-Host "Options:" -ForegroundColor Cyan
                 Write-Host "1. Wait 10-15 minutes and try again (Recommended)" -ForegroundColor White
                 Write-Host "2. Fallback to self-signed certificate (Not secure in browsers)" -ForegroundColor White
                 
                 $choice = Read-Host "Enter choice (1/2)"
                 if ($choice -ne "2") {
                     Write-Host "Exiting so you can retry later."
                     exit 1
                 }
            } else {
                 Write-Host ""
                 Write-Host "⚠️  Unknown error or feature not supported." -ForegroundColor Yellow
                 Write-Host "Attempting fallback..." -ForegroundColor Yellow
            }
            
            # Fallback: Create self-signed certificate in Key Vault
            Write-Host "Attempting fallback: Creating self-signed certificate in Key Vault..." -ForegroundColor Cyan
            Write-Host "This certificate will work for HTTPS but browsers will show warnings." -ForegroundColor Yellow
            Write-Host ""
            
            $keyVaultName = $resources["KeyVaultName"]
            $certName = $CustomDomain.Replace(".", "-")
            
            # Step 4a: Check if certificate already exists in Key Vault
            Write-Host "Checking for existing certificate in Key Vault ($keyVaultName)..." -ForegroundColor Gray
            $certExists = $false
            try {
                # Temporarily relax error action to handle 'Received 404' cleanly
                $oldEAP = $ErrorActionPreference
                $ErrorActionPreference = "Continue"
                
                $existingKvCert = az keyvault certificate show --vault-name $keyVaultName --name $certName 2>&1
                if ($LASTEXITCODE -eq 0) {
                    $certExists = $true
                }
                $ErrorActionPreference = $oldEAP
            } catch {
                $ErrorActionPreference = $oldEAP
                $certExists = $false
            }

            if ($certExists) {
                Write-Host "✓ Certificate already exists in Key Vault: $certName" -ForegroundColor Green
            } else {
                Write-Host "Creating self-signed certificate in Key Vault..." -ForegroundColor Cyan
                
                # Create certificate policy
                $policy = @{
                    "issuerParameters" = @{
                        "name" = "Self"
                    }
                    "keyProperties" = @{
                        "exportable" = $true
                        "keyType" = "RSA"
                        "keySize" = 2048
                        "reuseKey" = $true
                    }
                    "lifetimeActions" = @(
                        @{
                            "action" = @{
                                "actionType" = "AutoRenew"
                            }
                            "trigger" = @{
                                "daysBeforeExpiry" = 90
                            }
                        }
                    )
                    "secretProperties" = @{
                        "contentType" = "application/x-pkcs12"
                    }
                    "x509CertificateProperties" = @{
                        "subject" = "CN=$CustomDomain"
                        "subjectAlternativeNames" = @{
                            "dnsNames" = @($CustomDomain)
                        }
                        "keyUsage" = @("digitalSignature", "keyEncipherment")
                        "validityInMonths" = 12
                    }
                } | ConvertTo-Json -Depth 10
                
                $policyFile = [System.IO.Path]::GetTempFileName() + ".json"
                $policy | Out-File -FilePath $policyFile -Encoding UTF8
                
                try {
                    az keyvault certificate create `
                        --vault-name $keyVaultName `
                        --name $certName `
                        --policy "@$policyFile" `
                        2>&1 | Out-Null
                    
                    if ($LASTEXITCODE -eq 0) {
                        Write-Host "✓ Self-signed certificate created in Key Vault" -ForegroundColor Green
                        
                        # Wait for certificate to be ready
                        Write-Host "Waiting for certificate to be ready..." -ForegroundColor Gray
                        $maxWait = 30
                        $waited = 0
                        while ($waited -lt $maxWait) {
                            Start-Sleep -Seconds 2
                            $waited += 2
                            $certStatus = az keyvault certificate show --vault-name $keyVaultName --name $certName --query "attributes.enabled" -o tsv 2>&1
                            if ($certStatus -eq "true") {
                                Write-Host "✓ Certificate is ready" -ForegroundColor Green
                                break
                            }
                        }
                    } else {
                        Write-Host "Warning: Failed to create certificate in Key Vault" -ForegroundColor Yellow
                    }
                } finally {
                    Remove-Item -Path $policyFile -Force -ErrorAction SilentlyContinue
                }
            }
            
            # Step 4b: Import certificate from Key Vault to App Service
            Write-Host ""
            Write-Host "Step 6: Importing certificate from Key Vault to App Service..." -ForegroundColor Cyan
            
            $kvCertId = az keyvault certificate show --vault-name $keyVaultName --name $certName --query "id" -o tsv
            if ([string]::IsNullOrWhiteSpace($kvCertId)) {
                Write-Host "Error: Could not get certificate ID from Key Vault" -ForegroundColor Red
                Write-Host ""
                Write-Host "Manual steps required:" -ForegroundColor Yellow
                Write-Host "1. Go to Azure Portal → Key Vault → Certificates" -ForegroundColor White
                Write-Host "2. Create or upload a certificate for $CustomDomain" -ForegroundColor White
                Write-Host "3. Go to App Service → Certificates → Import from Key Vault" -ForegroundColor White
                Write-Host "4. Select the certificate and bind it to the custom domain" -ForegroundColor White
                exit 0
            }
            
            Write-Host "Certificate ID: $kvCertId" -ForegroundColor Gray
            
            # Import certificate to App Service using Key Vault reference
            try {
                $oldEAP = $ErrorActionPreference
                $ErrorActionPreference = "Continue" # Don't stop on warnings
                
                $importResult = az webapp config ssl import `
                    --name $webAppName `
                    --resource-group $resourceGroup `
                    --key-vault $keyVaultName `
                    --key-vault-certificate-name $certName `
                    2>&1
                
                $success = $LASTEXITCODE -eq 0
                $ErrorActionPreference = $oldEAP
            } catch {
                $ErrorActionPreference = $oldEAP
                $success = $false
                $importResult = $_
            }
            
            if (-not $success) {
                # Check if it was just a warning that caused non-zero exit or stderr output
                if ($importResult -match "WARNING" -and $importResult -match "permissions") {
                     Write-Host "Received warning about permissions but continuing check..." -ForegroundColor Gray
                } else {
                    Write-Host "Warning: Failed to import certificate from Key Vault" -ForegroundColor Yellow
                    Write-Host $importResult -ForegroundColor Gray
                    Write-Host ""
                    Write-Host "This might be due to permissions. Manual import required:" -ForegroundColor Yellow
                    Write-Host "1. Go to Azure Portal → App Service → Certificates" -ForegroundColor White
                    Write-Host "2. Click 'Import from Key Vault'" -ForegroundColor White
                    Write-Host "3. Select Key Vault: $keyVaultName" -ForegroundColor White
                    Write-Host "4. Select Certificate: $certName" -ForegroundColor White
                    exit 0
                }
            }
            
            Write-Host "✓ Certificate imported from Key Vault" -ForegroundColor Green
            
            # Step 4c: Get the thumbprint and bind
            Write-Host ""
            Write-Host "Step 7: Binding certificate to custom domain..." -ForegroundColor Cyan
            
            Start-Sleep -Seconds 2  # Give Azure time to process the import
            
            $importedCert = az webapp config ssl list `
                --resource-group $resourceGroup `
                -o json | ConvertFrom-Json | Where-Object { $_.name -like "*$certName*" } | Select-Object -First 1
            
            if ($null -eq $importedCert) {
                Write-Host "Warning: Certificate imported but not found in App Service" -ForegroundColor Yellow
                Write-Host "Please bind manually via Azure Portal" -ForegroundColor Yellow
                exit 0
            }
            
            $thumbprint = $importedCert.thumbprint
            Write-Host "Certificate thumbprint: $thumbprint" -ForegroundColor Gray
            
            az webapp config ssl bind `
                --name $webAppName `
                --resource-group $resourceGroup `
                --certificate-thumbprint $thumbprint `
                --ssl-type SNI `
                2>&1 | Out-Null
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✓ Self-signed certificate bound successfully!" -ForegroundColor Green
                Write-Host ""
                Write-Host "⚠️  NOTE: This is a self-signed certificate" -ForegroundColor Yellow
                Write-Host "   Browsers will show security warnings" -ForegroundColor Yellow
                Write-Host "   For production, replace with a CA-signed certificate" -ForegroundColor Yellow
            } else {
                Write-Host "Warning: Certificate import succeeded but binding failed" -ForegroundColor Yellow
                Write-Host "Please bind manually via Azure Portal" -ForegroundColor Yellow
            }
            exit 0
        }
        
        Write-Host "✓ Managed SSL certificate created successfully!" -ForegroundColor Green
        
        # Step 5: Bind the certificate
        Write-Host ""
        Write-Host "Step 5: Binding SSL certificate..." -ForegroundColor Cyan
        
        # Get the thumbprint of the newly created certificate
        $certInfo = az webapp config ssl list `
            --resource-group $resourceGroup `
            -o json | ConvertFrom-Json | Where-Object { $_.hostNames -contains $CustomDomain } | Select-Object -First 1
        
        if ($null -eq $certInfo) {
            Write-Host "Warning: Certificate was created but could not be found for binding" -ForegroundColor Yellow
        } else {
            $thumbprint = $certInfo.thumbprint
            Write-Host "  Certificate thumbprint: $thumbprint" -ForegroundColor Gray
            
            az webapp config ssl bind `
                --name $webAppName `
                --resource-group $resourceGroup `
                --certificate-thumbprint $thumbprint `
                --ssl-type SNI `
                2>&1 | Out-Null
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✓ SSL certificate bound successfully!" -ForegroundColor Green
            } else {
                Write-Host "Warning: Certificate created but binding failed" -ForegroundColor Yellow
            }
        }
    }
    
    Write-Host ""
    Write-Host "============================================" -ForegroundColor Green
    Write-Host "Custom Domain & SSL Configuration Complete!" -ForegroundColor Green
    Write-Host "============================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Your app is now accessible via HTTPS:" -ForegroundColor White
    Write-Host "  https://$CustomDomain/" -ForegroundColor Green
    Write-Host "  https://$CustomDomain/swagger" -ForegroundColor Green
    Write-Host "  https://$CustomDomain/api/health/check" -ForegroundColor Green
    Write-Host ""
    Write-Host "Note: It may take a few minutes for the SSL certificate to propagate." -ForegroundColor Yellow
    
    # Test the HTTPS endpoint
    Write-Host ""
    Write-Host "Testing HTTPS endpoint..." -ForegroundColor Cyan
    try {
        $response = Invoke-WebRequest -Uri "https://$CustomDomain/api/health/check" -UseBasicParsing -TimeoutSec 10 -ErrorAction Stop
        Write-Host "✓ HTTPS endpoint is working! Status: $($response.StatusCode)" -ForegroundColor Green
    } catch {
        Write-Host "HTTPS endpoint not yet accessible (this is normal, wait 2-3 minutes)" -ForegroundColor Yellow
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
    }
    
    Write-Host ""
    }
}

# ============================================================================
# SECTION 7: RUN DATABASE MIGRATIONS (if requested)
# ============================================================================

if ($RunMigrations) {
    Write-Host "🗄️ Running database migrations on $Environment..." -ForegroundColor Yellow
    
    try {
        $connectionString = az keyvault secret show --vault-name $resources["KeyVaultName"] --name "Sql__ConnectionString" --query value -o tsv
        if ($connectionString) {
            Write-Host "Running EF migrations..."
            
            $environmentLabel = $Environment.ToLower()
            if (Test-Path "publish-$environmentLabel/efbundle.exe") {
                ./publish-$environmentLabel/efbundle.exe --connection $connectionString
            } else {
                Write-Error "Migration bundle not found. Deploy application first."
                exit 1
            }
            
            Write-Host "✅ Database migrations completed!" -ForegroundColor Green
        } else {
            Write-Warning "Could not retrieve connection string from Key Vault"
        }
    } catch {
        Write-Error "Failed to run migrations: $($_.Exception.Message)"
    }
}

# ============================================================================
# SECTION 7: CLEANUP TEMPORARY ARTIFACTS
# ============================================================================

$environmentLabel = $Environment.ToLower()
$publishDir = "publish-$environmentLabel"
$deployZip = "deploy-$environmentLabel.zip"

if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
if (Test-Path $deployZip) { Remove-Item -Force $deployZip }

# ============================================================================
# SECTION 8: DEPLOYMENT SUMMARY
# ============================================================================

# Fetch hostname one last time to be sure
$finalHostName = az webapp show -n $resources["WebAppName"] -g $resources["ResourceGroup"] --query defaultHostName -o tsv

Write-Host ""
Write-Host "🎉 $Environment deployment completed!" -ForegroundColor Green
Write-Host "$Environment URL: https://$finalHostName/swagger" -ForegroundColor Cyan
$webAppName = $resources['WebAppName']
$resourceGroup = $resources['ResourceGroup']
Write-Host "Monitor logs: az webapp log tail -n $webAppName -g $resourceGroup" -ForegroundColor Gray

