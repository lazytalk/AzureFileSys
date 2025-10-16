#!/usr/bin/env pwsh
# deploy-staging.ps1 - Deploy File Service to Azure Staging Environment

# Saved/normalized: ensure file is written as UTF-8 without BOM when edited by the deploy tooling.

param(
    [switch]$CreateResources,
    [switch]$DeployApp,
    [switch]$RunMigrations,
    [string]$SubscriptionId = "090ad03d-c465-4d11-a048-571b892978f4",
    [string]$Location = "chinaeast",  # Azure China region
    [string]$ResourcePrefix = "filesvc-staging",
    [System.Management.Automation.PSCredential]$SqlAdminCredential = $null
)

# Staging Environment Variables
$resourceGroup = "KWE-ResourceGroup-ChinaNorth"

# Preflight diagnostics to help detect encoding/corruption when run remotely
try {
    $scriptPath = $MyInvocation.MyCommand.Path
    Write-Host "Running script: $scriptPath" -ForegroundColor Cyan
    if (Test-Path $scriptPath) {
        $bytes = [System.IO.File]::ReadAllBytes($scriptPath)
        $sha = [System.BitConverter]::ToString((New-Object System.Security.Cryptography.SHA256Managed).ComputeHash($bytes)).Replace('-','')
        Write-Host "Script SHA256: $sha" -ForegroundColor Gray
        # Check for BOM (0xEF,0xBB,0xBF)
        if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
            Write-Host "Warning: script file contains UTF-8 BOM which some PowerShell hosts mis-handle. Consider saving as UTF-8 (no BOM)." -ForegroundColor Yellow
        }
    }
} catch {
    Write-Host "Preflight diagnostics failed: $($_.Exception.Message)" -ForegroundColor Yellow
}


# Helper: sanitize names for services with strict naming rules (storage, keyvault, webapp)
function Sanitize-ResourceName {
    param(
        [string]$Name,
        [int]$MaxLength = 24
    )

    # Lowercase, replace invalid characters with dashes, collapse multiple dashes
    $san = $Name.ToLower() -replace '[^a-z0-9-]', '-' -replace '-{2,}', '-'
    $san = $san.Trim('-')
    if ($san.Length -gt $MaxLength) { $san = $san.Substring(0, $MaxLength) }
    return $san
}

$storageAccountRaw = ($ResourcePrefix -replace '-', '') + 'stg' + (Get-Random -Minimum 1000 -Maximum 9999)
$storageAccount = Sanitize-ResourceName -Name $storageAccountRaw -MaxLength 24
# Storage account names must contain only lowercase letters and numbers (no dashes)
$storageAccount = ($storageAccount -replace '-', '')
if ($storageAccount.Length -gt 24) { $storageAccount = $storageAccount.Substring(0,24) }
$webAppName = Sanitize-ResourceName -Name "${ResourcePrefix}-app" -MaxLength 60
$keyVaultName = Sanitize-ResourceName -Name "${ResourcePrefix}-kv" -MaxLength 24
$appInsightsName = Sanitize-ResourceName -Name "${ResourcePrefix}-ai" -MaxLength 24
$sqlServerName = Sanitize-ResourceName -Name "${ResourcePrefix}-sql" -MaxLength 63
$sqlDbName = "file-service-db"
$sqlAdminUser = "fsadmin"

# Accept either a SecureString or a plain string for SqlAdminPassword.
# Prefer a SecureString (recommended). If a plain string is provided we'll convert it,
# and if nothing is provided we prompt interactively.
if ($SqlAdminCredential -is [System.Management.Automation.PSCredential]) {
    $secureSqlAdminPassword = $SqlAdminCredential.Password
} else {
    Write-Host "No SQL admin credential supplied; prompting interactively (credential)." -ForegroundColor Yellow
    # Per convenience: prompt for SQL admin credential. This lets the user just run the script and enter values.
    $cred = Get-Credential -Message 'SQL admin credential (example: fsadmin)'
    $secureSqlAdminPassword = $cred.Password
    if ($cred.UserName) { $sqlAdminUser = $cred.UserName }
}

# Convert SecureString to plain text securely for az CLI usage, then zero the pointer
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureSqlAdminPassword)
$unsecureSqlAdminPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)

Write-Host "File Service - Staging Deployment" -ForegroundColor Cyan
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
        # App Service domains differ in Azure China (azurewebsites.cn)
        $script:WebAppDomainSuffix = 'azurewebsites.cn'
    }
}

# Ensure the user is logged into the correct Azure cloud and has an active session.
function Ensure-AzureAuthenticated {
    param(
        [string]$LocationHint
    )

    # If the location hints China, set the cloud name accordingly
    if ($LocationHint -and ($LocationHint -match 'china')) {
        Write-Host "Configuring Azure CLI for China cloud..." -ForegroundColor Yellow
        az cloud set --name AzureChinaCloud
        if (-not (az account show 2>$null)) {
            Write-Host "Not currently authenticated for Azure China; launching 'az login'..." -ForegroundColor Yellow
            az login
        } else {
            Write-Host "Already authenticated to Azure (China cloud)." -ForegroundColor Gray
        }
        $script:WebAppDomainSuffix = 'azurewebsites.cn'
    } else {
        # Ensure global cloud is set if needed
        if (-not (az account show 2>$null)) {
            Write-Host "Not currently authenticated to Azure; launching 'az login'..." -ForegroundColor Yellow
            az login
        } else {
            Write-Host "Already authenticated to Azure." -ForegroundColor Gray
        }
    }
}

# Ensure authentication before proceeding
Ensure-AzureAuthenticated -LocationHint $Location

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
    Write-Host "Creating Azure resources for staging environment..." -ForegroundColor Yellow
    
    # Helper: ensure resource provider (Microsoft.KeyVault) is registered in this subscription
    function Ensure-ProviderRegistered {
        param(
            [string]$ProviderNamespace = 'Microsoft.KeyVault'
        )

        try {
            $prov = az provider show --namespace $ProviderNamespace -o json | ConvertFrom-Json
        } catch {
            Write-Host "Could not query provider '$ProviderNamespace': $($_.Exception.Message)" -ForegroundColor Yellow
            return $false
        }

        if ($prov.registrationState -ne 'Registered') {
            Write-Host ("Resource provider '" + $ProviderNamespace + "' is not registered (state: " + $prov.registrationState + "). Attempting to register...") -ForegroundColor Yellow
            az provider register --namespace $ProviderNamespace | Out-Null
            # Wait a short while for registration to complete
            $wait = 0
            while ($wait -lt 30) {
                Start-Sleep -Seconds 2
                $prov = az provider show --namespace $ProviderNamespace -o json | ConvertFrom-Json
                if ($prov.registrationState -eq 'Registered') { Write-Host "Provider '$ProviderNamespace' registered." -ForegroundColor Green; return $true }
                $wait += 2
            }
            Write-Host ("Provider '" + $ProviderNamespace + "' did not reach 'Registered' state after waiting.") -ForegroundColor Yellow
            return $false
        } else {
            Write-Host "Resource provider 'Microsoft.KeyVault' is already registered." -ForegroundColor Gray
            return $true
        }
    }

    # Attempt provider registration proactively to avoid create failures
    Ensure-ProviderRegistered -ProviderNamespace 'Microsoft.KeyVault' | Out-Null
    
    # Create staging resource group
    Write-Host "Creating resource group: $resourceGroup"
    az group create -n $resourceGroup -l $Location

    # Create Application Insights
    Write-Host "Creating Application Insights: $appInsightsName"
    az monitor app-insights component create -a $appInsightsName -g $resourceGroup -l $Location --application-type web
    $aiKey = az monitor app-insights component show -a $appInsightsName -g $resourceGroup --query instrumentationKey -o tsv

    # Create storage account
    Write-Host "Creating storage account: $storageAccount"
    az storage account create -n $storageAccount -g $resourceGroup -l "chinaeast2" --sku Standard_LRS --kind StorageV2
    az storage container create --account-name $storageAccount -n userfiles-staging --auth-mode key --public-access off
    $storageConnString = az storage account show-connection-string -n $storageAccount -g $resourceGroup -o tsv

    # Create SQL Server and Database (Basic tier for staging)
    Write-Host "Creating Azure SQL Database server: $sqlServerName"
    $sqlCreated = $true
    try {
        az sql server create -n $sqlServerName -g $resourceGroup -l $Location --admin-user $sqlAdminUser --admin-password $unsecureSqlAdminPassword | Out-Null
        az sql db create -s $sqlServerName -g $resourceGroup -n $sqlDbName --service-objective Basic | Out-Null
        az sql server firewall-rule create -s $sqlServerName -g $resourceGroup -n AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0 | Out-Null
        # Get ADO.NET connection string template and replace placeholders safely
        $sqlConnTpl = az sql db show-connection-string -s $sqlServerName -n $sqlDbName -c ado.net -o tsv
        $sqlConnString = $sqlConnTpl -replace '<username>', $sqlAdminUser -replace '<password>', $unsecureSqlAdminPassword
    } catch {
        Write-Host "SQL server creation failed: $($_.Exception.Message)" -ForegroundColor Yellow
        if ($_.Exception.Message -match 'RegionDoesNotAllowProvisioning') {
            Write-Host "Region '$Location' is temporarily not allowing new SQL servers. Choose a different location or try again later." -ForegroundColor Yellow
        }
        $sqlCreated = $false
    }

    # Create Key Vault with retries and verify readiness (Azure can be eventually consistent)
    Write-Host "Creating Key Vault: $keyVaultName"
    $kvCreateAttempts = 0
    $kvCreated = $false
    while (-not $kvCreated -and $kvCreateAttempts -lt 3) {
        $kvCreateAttempts++
            try {
                # Some CLI versions (and Azure China clouds) don't accept boolean 'true' after flags; pass flags without values.
                az keyvault create -n $keyVaultName -g $resourceGroup -l $Location --enable-soft-delete | Out-Null
            } catch {
                Write-Host "Key Vault create attempt ${kvCreateAttempts} failed: $($_.Exception.Message)" -ForegroundColor Yellow
            }

        # Wait for the Key Vault to be visible
        $checkAttempts = 0
        while ($checkAttempts -lt 6) {
            $checkAttempts++
            try {
                $kvUri = az keyvault show -n $keyVaultName -g $resourceGroup --query properties.vaultUri -o tsv 2>$null
                if ($kvUri) { $kvCreated = $true; break }
            } catch {
                Write-Host "Key Vault show attempt ${checkAttempts} returned error: $($_.Exception.Message)" -ForegroundColor Yellow
            }
            Start-Sleep -Seconds (5 * $checkAttempts)
        }

        if (-not $kvCreated) {
            Write-Host "Key Vault not available after create attempt ${kvCreateAttempts}; will retry create." -ForegroundColor Yellow
        }
    }

    if (-not $kvCreated) {
        Write-Error "Failed to create Key Vault '$keyVaultName' after ${kvCreateAttempts} attempts. Check permissions and Azure service availability." 
        exit 1
    }

    # Now ensure Key Vault DNS resolves and is reachable
    $maxAttempts = 12
    $attempt = 0
    $kvReady = $false
    while ($attempt -lt $maxAttempts -and -not $kvReady) {
        try {
            $attempt++
            Write-Host ("Checking Key Vault readiness (attempt " + $attempt + "/" + $maxAttempts + ")...") -ForegroundColor Gray
            $kvUri = az keyvault show -n $keyVaultName -g $resourceGroup --query properties.vaultUri -o tsv 2>$null
            if ($kvUri) {
                # Try DNS resolution
                $hostName = ($kvUri -replace '^https?://','')
                try {
                    Resolve-DnsName $hostName -ErrorAction Stop | Out-Null
                    $kvReady = $true
                    break
                } catch {
                    Write-Host ("Key Vault DNS not yet resolvable: " + $hostName) -ForegroundColor Yellow
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

    # Helper: set a secret with retries and verification
    function Set-KeyVaultSecretWithRetry {
        param(
            [string]$VaultName,
            [string]$SecretName,
            [string]$SecretValue,
            [int]$MaxAttempts = 5
        )

        $ok = $false
        for ($i = 1; $i -le $MaxAttempts; $i++) {
            try {
                az keyvault secret set --vault-name $VaultName -n $SecretName --value "$SecretValue" | Out-Null
            } catch {
                Write-Host "Attempt ${i}: failed to set secret '$SecretName': $($_.Exception.Message)" -ForegroundColor Yellow
            }

            Start-Sleep -Seconds (3 * $i)
            try {
                $val = az keyvault secret show --vault-name $VaultName --name $SecretName --query value -o tsv 2>$null
                if ($val) { $ok = $true; break }
            } catch {
                Write-Host "Attempt ${i}: secret '$SecretName' not visible yet." -ForegroundColor Yellow
            }
        }

        if (-not $ok) {
            Write-Error "Failed to set or verify secret '$SecretName' in vault '$VaultName' after $MaxAttempts attempts."
            exit 1
        }
    }

    # Now set secrets (with verification)
    Set-KeyVaultSecretWithRetry -VaultName $keyVaultName -SecretName 'BlobStorage--ConnectionString' -SecretValue $storageConnString
    if ($sqlCreated) {
        Set-KeyVaultSecretWithRetry -VaultName $keyVaultName -SecretName 'Sql--ConnectionString' -SecretValue $sqlConnString
    } else {
        Write-Host "Skipping Sql--ConnectionString secret because SQL server was not created." -ForegroundColor Yellow
    }
    Set-KeyVaultSecretWithRetry -VaultName $keyVaultName -SecretName 'ApplicationInsights--InstrumentationKey' -SecretValue $aiKey
    # Optional: set external auth provider secrets if integrating with external auth
    # az keyvault secret set --vault-name $keyVaultName -n ExternalAuth--BaseUrl --value "https://test-auth.school.edu"
    # az keyvault secret set --vault-name $keyVaultName -n ExternalAuth--ApiKey --value "staging-api-key-placeholder"

    # Create App Service (Basic tier for staging)
    Write-Host "Creating App Service Plan and Web App: $webAppName"
    $appServicePlan = "${ResourcePrefix}-plan"
    $appServicePlan = Sanitize-ResourceName -Name $appServicePlan -MaxLength 45
    az appservice plan create -n $appServicePlan -g $resourceGroup --sku B1 --is-linux false
    az webapp create -n $webAppName -g $resourceGroup -p $appServicePlan --runtime 'DOTNET|8.0'

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

    if (-not $script:WebAppDomainSuffix) { $script:WebAppDomainSuffix = 'azurewebsites.net' }
    Write-Host "Staging resources created successfully!" -ForegroundColor Green
    Write-Host "Staging URL: https://$webAppName.$script:WebAppDomainSuffix" -ForegroundColor Green
}

if ($DeployApp) {
    Write-Host "Deploying application to staging..." -ForegroundColor Yellow
    
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
    
    Write-Host "Application deployed to staging!" -ForegroundColor Green
}

if ($RunMigrations) {
    Write-Host "Running database migrations on staging..." -ForegroundColor Yellow
    
    # Get connection string from Key Vault and run migrations
    try {
        $connectionString = az keyvault secret show --vault-name $keyVaultName --name "Sql--ConnectionString" --query value -o tsv 2>$null
        if ($connectionString) {
            Write-Host "Running EF migrations..."
            # Use call operator and full path; ensure correct quoting for Windows
            $efPath = Join-Path -Path (Get-Location).Path -ChildPath 'publish-staging\efbundle.exe'
            if (Test-Path $efPath) {
                & $efPath --connection "$connectionString"
                Write-Host "Database migrations completed!" -ForegroundColor Green
            } else {
                Write-Error "Migration bundle not found at $efPath. Ensure the DeployApp step produced the bundle or create one locally."
                exit 1
            }
        } else {
            Write-Error "Could not retrieve connection string from Key Vault '$keyVaultName'. DNS/networking or Key Vault permissions may be the issue."
            exit 1
        }
    } catch {
        Write-Error "Failed to run migrations: $($_.Exception.Message)"
    }
}

Write-Host ""
if (-not $script:WebAppDomainSuffix) { $script:WebAppDomainSuffix = 'azurewebsites.net' }
Write-Host "Staging deployment completed!" -ForegroundColor Green
Write-Host "Staging Swagger URL: https://$webAppName.$script:WebAppDomainSuffix/swagger" -ForegroundColor Cyan
Write-Host "Monitor logs: az webapp log tail -n $webAppName -g $resourceGroup" -ForegroundColor Gray

# Cleanup temp files
if (Test-Path "publish-staging") { Remove-Item -Recurse -Force publish-staging }
if (Test-Path "deploy-staging.zip") { Remove-Item -Force deploy-staging.zip }

# Clear sensitive variables from memory
if ($unsecureSqlAdminPassword) { $unsecureSqlAdminPassword = $null }
