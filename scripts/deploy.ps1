#!/usr/bin/env pwsh
# deploy.ps1 - Unified deployment script for staging and production environments
#
# This script handles:
# - Azure resource creation (Storage, Key Vault, App Service, etc.)
# - Application deployment
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
# 3. Configure custom domain with SSL (uses domain from deploy-settings.ps1):
#    .\deploy.ps1 -Environment Staging -ConfigureCustomDomain
#
# 4. Configure custom domain with SSL (override with specific domain):
#    .\deploy.ps1 -Environment Staging -ConfigureCustomDomain -CustomDomain "custom.example.com"
#
# 5. Full deployment with custom domain:
#    .\deploy.ps1 -Environment Production -CreateResources -DeployApp -ConfigureCustomDomain
#
# 6. Promote staging to production:
#    .\deploy.ps1 -Environment Production -PromoteFromStaging

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Staging", "Production")]
    [string]$Environment,
    
    [switch]$CreateResources,
    [switch]$DeployApp,
    [switch]$PromoteFromStaging,
    [switch]$ConfigureCustomDomain,
    [string]$CustomDomain = "",  # e.g., "filesvc-stg-app.kaiweneducation.com"
    [string]$SubscriptionId = "",
    [string]$Location = "",
    [string]$CertificatePassword = ""  # Optional: provide .pfx password non-interactively
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
$config = & (Join-Path $PSScriptRoot "deploy-settings.ps1") -Environment $Environment
$resources = $config.Resources
$appSettings = $config.AppSettings

# Apply Location override if provided via command line
if (-not [string]::IsNullOrWhiteSpace($Location)) {
    $resources["Location"] = $Location
}

# Set subscription if provided; otherwise use settings value if present
if (-not [string]::IsNullOrWhiteSpace($SubscriptionId)) {
    Write-Host "Setting Azure subscription: $SubscriptionId"
    az account set --subscription $SubscriptionId
} elseif (-not [string]::IsNullOrWhiteSpace($resources["SubscriptionId"])) {
    Write-Host "Setting Azure subscription from settings: $($resources["SubscriptionId"])"
    az account set --subscription $resources["SubscriptionId"]
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
    $appServiceSku = $resources["AppServiceSku"]
    $appServicePlanName = $resources["AppServicePlanName"]
    $envLabel = $resources["EnvLabel"]
    $tableName = $resources["TableName"]
    
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
        
        # Configure CORS for Direct-to-Blob uploads (Staging and Production)
        Write-Host "Configuring CORS for Blob Storage to allow direct uploads..." -ForegroundColor Gray
        $corsOriginsRaw = $appSettings["Cors__AllowedOrigins"]
        $corsOrigins = @()
        if (-not [string]::IsNullOrWhiteSpace($corsOriginsRaw)) {
            $corsOrigins = $corsOriginsRaw.Split(';') | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        }
        if ($corsOrigins.Count -eq 0) {
            Write-Host "⚠ No CORS origins configured; skipping blob CORS setup" -ForegroundColor Yellow
        } else {
            az storage cors clear --account-name $storageAccount --services b
            az storage cors add --account-name $storageAccount --services b --origins ($corsOrigins -join ',') --methods DELETE GET HEAD MERGE POST OPTIONS PUT --allowed-headers "*" --exposed-headers "*" --max-age 86400
        }
    }
    $storageConnString = az storage account show-connection-string -n $storageAccount -g $resourceGroup -o tsv
    
    # Table Storage uses the same connection string as blob storage
    Write-Host "✓ Table Storage will use storage account connection string" -ForegroundColor Green
    
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
    az keyvault secret set --vault-name $keyVaultName -n "TableStorage--ConnectionString" --value $storageConnString > $null
    az keyvault secret set --vault-name $keyVaultName -n "ApplicationInsights--InstrumentationKey" --value $aiKey > $null
    az keyvault secret set --vault-name $keyVaultName -n "PowerSchool--BaseUrl" --value $resources["PowerSchoolBaseUrl"] > $null
    az keyvault secret set --vault-name $keyVaultName -n "PowerSchool--ApiKey" --value "$envLabel-api-key-placeholder" > $null
    
    # Set or update HMAC shared secret for API request signature validation
    $hmacSecret = $resources["HmacSharedSecret"]
    if ([string]::IsNullOrWhiteSpace($hmacSecret)) {
        # Generate a random 32-byte secret if not provided
        $hmacSecret = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
        Write-Host "Generated new HMAC shared secret" -ForegroundColor Yellow
    }
    az keyvault secret set --vault-name $keyVaultName -n "Security--HmacSharedSecret" --value $hmacSecret > $null
    Write-Host "✓ HMAC shared secret configured" -ForegroundColor Green
    
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
    $finalSettings["TableStorage__ConnectionString"] = "@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=TableStorage--ConnectionString)"
    $finalSettings["ApplicationInsights__InstrumentationKey"] = "@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=ApplicationInsights--InstrumentationKey)"
    $finalSettings["PowerSchool__BaseUrl"] = "@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=PowerSchool--BaseUrl)"
    $finalSettings["PowerSchool__ApiKey"] = "@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=PowerSchool--ApiKey)"
    $finalSettings["Security__HmacSharedSecret"] = "@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=Security--HmacSharedSecret)"

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
    $scriptDir = $PSScriptRoot
    $rootDir = Split-Path $scriptDir -Parent
    $publishDir = Join-Path $rootDir "publish-$environmentLabel"
    $deployZip = Join-Path $rootDir "deploy-$environmentLabel.zip"
    $projectFile = Join-Path $rootDir "src\FileService.Api\FileService.Api.csproj"
    
    # Build and publish
    Write-Host "Building application..."
    dotnet publish $projectFile -c Release -o $publishDir --verbosity quiet
    
    # Create deployment package
    Write-Host "Creating deployment package..."
    Push-Location $publishDir
    Compress-Archive -Path * -DestinationPath $deployZip -Force
    Pop-Location
    
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
    
    # Check if we have a .pfx file to import
    $certFolder = Join-Path (Split-Path -Parent $PSScriptRoot) "certificates"
    $shouldReplaceCert = $false
    
    if (Test-Path $certFolder) {
        $preferredPfxName = $resources["CertificatePfxFileName"]
        $pfxFile = $null

        if (-not [string]::IsNullOrWhiteSpace($preferredPfxName)) {
            $candidatePath = Join-Path $certFolder $preferredPfxName
            if (Test-Path $candidatePath) {
                $pfxFile = Get-Item $candidatePath
                $shouldReplaceCert = $true
            }
        }

        if (-not $pfxFile) {
            $pfxFiles = Get-ChildItem -Path $certFolder -Filter "*.pfx" -ErrorAction SilentlyContinue
            if ($pfxFiles -and $pfxFiles.Count -gt 0) {
                $pfxFile = $pfxFiles | Select-Object -First 1
                $shouldReplaceCert = $true
            }
        }
    }
    
    $certificates = az webapp config ssl list --resource-group $resourceGroup -o json | ConvertFrom-Json
    $existingCert = $certificates | Where-Object { $_.hostNames -contains $CustomDomain }
    $existingSelfSigned = $false
    if ($existingCert) {
        $issuer = $existingCert.issuer
        $subject = $existingCert.subjectName
        if (-not [string]::IsNullOrWhiteSpace($issuer) -and -not [string]::IsNullOrWhiteSpace($subject)) {
            if ($issuer -eq $subject -or $issuer -like "*$CustomDomain*") {
                $existingSelfSigned = $true
            }
        }
    }
    
    if ($existingCert -and $existingSelfSigned) {
        if ($pfxFile) {
            $shouldReplaceCert = $true
            Write-Host "Existing certificate appears self-signed; will replace with provided CA certificate" -ForegroundColor Yellow
        } else {
            Write-Host "Error: Existing certificate for $CustomDomain appears self-signed. Provide a CA-issued .pfx in $certFolder or via -CertificatePassword." -ForegroundColor Red
            exit 1
        }
    }

    if ($existingCert -and -not $shouldReplaceCert) {
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
        # Import/replace certificate from .pfx file
        if ($existingCert -and $shouldReplaceCert) {
            Write-Host "Existing certificate found - will replace with new certificate from .pfx file" -ForegroundColor Yellow
            Write-Host "  Current Thumbprint: $($existingCert.thumbprint)" -ForegroundColor Gray
            
            # Unbind existing certificate
            Write-Host "Unbinding existing certificate..." -ForegroundColor Cyan
            az webapp config ssl unbind `
                --name $webAppName `
                --resource-group $resourceGroup `
                --certificate-thumbprint $($existingCert.thumbprint) `
                2>&1 | Out-Null
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✓ Existing certificate unbound" -ForegroundColor Green
            }
        } else {
            Write-Host "No SSL certificate found on App Service." -ForegroundColor Yellow
        }
        
        Write-Host "Locating certificate file..." -ForegroundColor Cyan

        if (-not $pfxFile) {
            Write-Host "Error: No .pfx certificate file found in certificates folder." -ForegroundColor Red
            Write-Host "Expected location: $certFolder" -ForegroundColor Yellow
            if (-not [string]::IsNullOrWhiteSpace($preferredPfxName)) {
                Write-Host "Looking for file named: $preferredPfxName" -ForegroundColor Yellow
            }
            exit 1
        }

        Write-Host "Found certificate: $($pfxFile.Name)" -ForegroundColor Green

        # Determine certificate password: CLI param -> settings -> prompt
        $certPasswordPlain = $CertificatePassword
        if ([string]::IsNullOrWhiteSpace($certPasswordPlain)) {
            $certPasswordPlain = $resources["CertificatePassword"]
        }
        if ([string]::IsNullOrWhiteSpace($certPasswordPlain)) {
            $certPassword = Read-Host "Enter certificate password for $($pfxFile.Name)" -AsSecureString
            $certPasswordPlain = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto([System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($certPassword))
        }

        # Use a Key Vault-friendly certificate name (no dots)
        $certName = ($CustomDomain -replace '\.', '-')
        $keyVaultName = $resources["KeyVaultName"]

        # Always import/replace certificate in Key Vault with current .pfx file
        Write-Host "Importing certificate into Key Vault ($keyVaultName) as '$certName' (will replace if exists)..." -ForegroundColor Cyan
        $kvResult = az keyvault certificate import `
            --vault-name $keyVaultName `
            --name $certName `
            --file $pfxFile.FullName `
            --password $certPasswordPlain `
            2>&1
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Error importing certificate into Key Vault:" -ForegroundColor Red
            Write-Host $kvResult -ForegroundColor Red
            exit 1
        }
        Write-Host "✓ Certificate stored in Key Vault" -ForegroundColor Green

        Write-Host "Importing certificate from Key Vault to App Service..." -ForegroundColor Cyan
        
        # Grant necessary Key Vault permissions for certificate import
        Write-Host "Configuring Key Vault RBAC permissions..." -ForegroundColor Gray
        
        # Get the web app's managed identity principal ID
        $webAppPrincipalId = az webapp identity show --name $webAppName --resource-group $resourceGroup --query principalId -o tsv
        
        # Get the Microsoft Azure App Service service principal object ID
        $appServiceSpObjectId = az ad sp list --filter "appId eq 'abfa0a7c-a6b6-4736-8310-5855508787cd'" --query "[0].id" -o tsv
        
        # Get current subscription ID
        $subscriptionId = az account show --query id -o tsv
        $kvResourceId = "/subscriptions/$subscriptionId/resourceGroups/$resourceGroup/providers/Microsoft.KeyVault/vaults/$keyVaultName"
        
        # Grant Key Vault Secrets User to web app managed identity
        Write-Host "  Granting Key Vault Secrets User to web app identity..." -ForegroundColor Gray
        az role assignment create --assignee $webAppPrincipalId --role "Key Vault Secrets User" --scope $kvResourceId 2>&1 | Out-Null
        
        # Grant Key Vault Certificates Officer to web app managed identity
        Write-Host "  Granting Key Vault Certificates Officer to web app identity..." -ForegroundColor Gray
        az role assignment create --assignee $webAppPrincipalId --role "Key Vault Certificates Officer" --scope $kvResourceId 2>&1 | Out-Null
        
        # Grant Key Vault Secrets User to Microsoft Azure App Service service principal
        Write-Host "  Granting Key Vault Secrets User to Azure App Service..." -ForegroundColor Gray
        az role assignment create --assignee $appServiceSpObjectId --role "Key Vault Secrets User" --scope $kvResourceId 2>&1 | Out-Null
        
        Write-Host "Waiting 60s for RBAC propagation..." -ForegroundColor Gray
        Start-Sleep -Seconds 60
        
        $thumbprint = az webapp config ssl import `
            --name $webAppName `
            --resource-group $resourceGroup `
            --key-vault $keyVaultName `
            --key-vault-certificate-name $certName `
            --query thumbprint `
            -o tsv `
            2>&1

        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($thumbprint)) {
            Write-Host "Error importing certificate from Key Vault to App Service:" -ForegroundColor Red
            Write-Host $thumbprint -ForegroundColor Red
            exit 1
        }
        
        Write-Host "✓ Certificate imported to App Service. Thumbprint: $thumbprint" -ForegroundColor Green
        
        # Bind
        Write-Host "Binding certificate..." -ForegroundColor Cyan
        az webapp config ssl bind `
            --name $webAppName `
            --resource-group $resourceGroup `
            --certificate-thumbprint $thumbprint `
            --ssl-type SNI `
            2>&1 | Out-Null
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ SSL certificate bound successfully!" -ForegroundColor Green
        } else {
            Write-Host "Warning: Failed to bind certificate" -ForegroundColor Yellow
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

