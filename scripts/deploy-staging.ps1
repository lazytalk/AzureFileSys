#!/usr/bin/env pwsh
# Working Azure China deployment script with Table Storage
# This script deploys the File Service to Azure China Cloud

param(
    [string]$SubscriptionId,
    [string]$Location = "chinaeast",
    [string]$ResourcePrefix = "filesvc-stg",
    [string]$CustomDomain = "kaiweneducation.com",
    [string]$CertificateFile,
    [string]$CertificatePassword,
    [switch]$UseLetsEncrypt
)

$ResourceGroup = "${ResourcePrefix}-rg"
$storageAccount = "filesvcstg" + $Location
$keyVaultName = "${ResourcePrefix}-kv" 
$appInsightsName = "${ResourcePrefix}-ai"
$webAppName = "${ResourcePrefix}-app"
$appServicePlan = "${ResourcePrefix}-plan"
$containerName = "userfiles-staging"

Write-Host "=== Azure File Service Deployment (China Cloud + Table Storage) ===" -ForegroundColor Cyan

# Step 1: Setup Azure China Cloud
Write-Host "Setting up Azure China Cloud..." -ForegroundColor Yellow
az cloud set --name AzureChinaCloud

if (-not $SubscriptionId) {
    $SubscriptionId = az account show --query id --output tsv
}

az account set --subscription $SubscriptionId
$subscriptionName = az account show --query name --output tsv
Write-Host "Using subscription: $subscriptionName" -ForegroundColor Green

Write-Host "Configuration:" -ForegroundColor Gray
Write-Host "  Resource Group: $ResourceGroup"
Write-Host "  Location: $Location"
Write-Host "  Storage Account: $storageAccount"
Write-Host "  Web App: $webAppName"
Write-Host ""

# Step 2: Create Resource Group
Write-Host "Creating Resource Group..." -ForegroundColor Yellow
$rgExists = az group exists --name $ResourceGroup
if ($rgExists -eq "true") {
    Write-Host "Resource group already exists" -ForegroundColor Green
} else {
    az group create --name $ResourceGroup --location $Location | Out-Null
    Write-Host "Resource group created" -ForegroundColor Green
}

# Step 3: Create Storage Account
Write-Host "Creating Storage Account..." -ForegroundColor Yellow
$storageExists = az storage account show --name $storageAccount --resource-group $ResourceGroup 2>$null
if ($storageExists) {
    Write-Host "Storage account already exists" -ForegroundColor Green
} else {
    az storage account create --name $storageAccount --resource-group $ResourceGroup --location $Location --sku Standard_LRS --kind StorageV2 | Out-Null
    Write-Host "Storage account created" -ForegroundColor Green
}

$storageConnString = az storage account show-connection-string --name $storageAccount --resource-group $ResourceGroup --output tsv

# Step 4: Create Key Vault
Write-Host "Creating Key Vault..." -ForegroundColor Yellow
$kvExists = az keyvault show --name $keyVaultName 2>$null
if ($kvExists) {
    Write-Host "Key Vault already exists" -ForegroundColor Green
} else {
    az keyvault create --name $keyVaultName --resource-group $ResourceGroup --location $Location | Out-Null
    Start-Sleep -Seconds 10
    Write-Host "Key Vault created" -ForegroundColor Green
}

# Note: secrets are written after RBAC for the deploying principal and the WebApp identity
# to avoid Forbidden errors while RBAC propagates.

# Step 6: Create App Service Plan
Write-Host "Creating App Service Plan..." -ForegroundColor Yellow
$planExists = az appservice plan show --name $appServicePlan --resource-group $ResourceGroup 2>$null
if ($planExists) {
    Write-Host "App Service Plan already exists" -ForegroundColor Green
} else {
    az appservice plan create --name $appServicePlan --resource-group $ResourceGroup --sku B1 | Out-Null
    Write-Host "App Service Plan created" -ForegroundColor Green
}

# Step 7: Create Web App
Write-Host "Creating Web App..." -ForegroundColor Yellow
$webExists = az webapp show --name $webAppName --resource-group $ResourceGroup 2>$null
if ($webExists) {
    Write-Host "Web App already exists" -ForegroundColor Green
} else {
    az webapp create --name $webAppName --resource-group $ResourceGroup --plan $appServicePlan --runtime "dotnet:8" | Out-Null
    Write-Host "Web App created" -ForegroundColor Green
}

# Verify configured runtime for the Web App (helpful sanity check)
try {
    $siteRuntime = az webapp show --name $webAppName --resource-group $ResourceGroup --query "siteConfig.linuxFxVersion" -o tsv 2>$null
    if ($siteRuntime) { Write-Host "Web App runtime (linuxFxVersion): $siteRuntime" -ForegroundColor Green }
    else { Write-Host "Could not read linuxFxVersion for Web App (it may be a Windows app)." -ForegroundColor Yellow }
} catch { Write-Host "Failed to query Web App runtime: $_" -ForegroundColor Yellow }

# Step 8: Configure Managed Identity
Write-Host "Configuring Managed Identity..." -ForegroundColor Yellow
$identity = az webapp identity show --name $webAppName --resource-group $ResourceGroup --query principalId --output tsv 2>$null
if (-not $identity) {
    az webapp identity assign --name $webAppName --resource-group $ResourceGroup | Out-Null
    Start-Sleep -Seconds 10
    $identity = az webapp identity show --name $webAppName --resource-group $ResourceGroup --query principalId --output tsv
}

$kvScope = az keyvault show --name $keyVaultName --resource-group $ResourceGroup --query id --output tsv
# Try to set access policy for the Web App identity (for Key Vaults using access policy model).
# This is best-effort: it will succeed for access-policy vaults and quietly continue for RBAC-enabled vaults.
Write-Host "Attempting to set Key Vault access policy for Web App identity (best-effort)..." -ForegroundColor Yellow
az keyvault set-policy --name $keyVaultName --object-id $identity --secret-permissions get list 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "Key Vault access policy set for Web App identity" -ForegroundColor Green
} else {
    Write-Host "Could not set access policy (vault may be RBAC-enabled or you may lack permissions). Continuing." -ForegroundColor Yellow
}

# Assign RBAC role (necessary for RBAC-enabled vaults)
az role assignment create --assignee $identity --role "Key Vault Secrets Officer" --scope $kvScope | Out-Null

Write-Host "Managed Identity configured" -ForegroundColor Green

try {
    # Also assign the current deploying principal permissions so the script can set secrets immediately
    $caller = az account show --query user.name --output tsv 2>$null
    if ($caller) {
        Write-Host "Granting Key Vault access policy to deploying principal: $caller" -ForegroundColor Yellow
        # Prefer UPN if it's an email, otherwise try SPN or objectId
        if ($caller -match "@") {
            # Try to set a keyvault access policy; for RBAC enabled vaults this will fail.
            $setPolicyResult = az keyvault set-policy --name $keyVaultName --upn $caller --secret-permissions get set list 2>$null | Out-Null
            if ($LASTEXITCODE -ne 0) {
                Write-Host "Could not set access policy (vault may be RBAC enabled). Attempting role assignment to user objectId instead..." -ForegroundColor Yellow
                $callerId = az ad user show --id $caller --query objectId -o tsv 2>$null
                if ($callerId) {
                    az role assignment create --assignee $callerId --role "Key Vault Secrets Officer" --scope $kvScope | Out-Null
                }
            }
        } else {
            # try as service principal name or object id via role assignment
            $callerId = $null
            try { $callerId = az ad sp show --id $caller --query objectId -o tsv 2>$null } catch {}
            if (-not $callerId) {
                try { $callerId = az ad user show --id $caller --query objectId -o tsv 2>$null } catch {}
            }
            if ($callerId) {
                az role assignment create --assignee $callerId --role "Key Vault Secrets Officer" --scope $kvScope | Out-Null
            }
        }
    }
} catch {
    Write-Host "Could not assign role to deploying principal (may be a service principal or lack permissions). Skipping." -ForegroundColor Yellow
}

# Wait for RBAC propagation for the WebApp identity
Write-Host "Waiting for Key Vault role/policy propagation (this can take a minute)..." -ForegroundColor Yellow
$propOk = $false
# Increase attempts to give RBAC/KeyVault propagation more time
for ($attempt=1; $attempt -le 20; $attempt++) {
    $assignments = az role assignment list --scope $kvScope --assignee $identity --query "[].roleDefinitionName" --output tsv 2>$null
    if ($assignments -and $assignments -match "Key Vault Secrets") {
        $propOk = $true
        break
    }
    Start-Sleep -Seconds 10
}

if (-not $propOk) {
    Write-Warning "RBAC assignment may not have propagated yet. Continuing but Key Vault secret set may fail."
}

# Now store secrets (after attempting RBAC assignment)
Write-Host "Storing secrets..." -ForegroundColor Yellow
az keyvault secret set --vault-name $keyVaultName --name "BlobStorage-ConnectionString" --value $storageConnString | Out-Null
az keyvault secret set --vault-name $keyVaultName --name "TableStorage-ConnectionString" --value $storageConnString | Out-Null
Write-Host "Secrets stored (or attempted)" -ForegroundColor Green

# Step 9: Configure App Settings
Write-Host "Configuring App Settings..." -ForegroundColor Yellow

$kvBlobRef = "@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=BlobStorage-ConnectionString)"
$kvTableRef = "@Microsoft.KeyVault(VaultName=$keyVaultName;SecretName=TableStorage-ConnectionString)"

# Use REST API for China Cloud compatibility
# Only set essential runtime settings and secrets - all other config comes from appsettings.Staging.json
$appSettingsBody = @{
    properties = @{
        "ASPNETCORE_ENVIRONMENT" = "Staging"
        "BlobStorage__ConnectionString" = $kvBlobRef
        "TableStorage__ConnectionString" = $kvTableRef
        # Make the app listen on port 5543 (Kestrel) and expose that port to the App Service front-end
        "WEBSITES_PORT" = "5543"
        "ASPNETCORE_URLS" = "http://+:5543"
    }
}

# Use management token + Invoke-RestMethod for reliable content-type handling in China Cloud
$jsonBody = $appSettingsBody | ConvertTo-Json -Depth 4
$token = az account get-access-token --resource https://management.chinacloudapi.cn/ --query accessToken -o tsv
if (-not $token) {
    Write-Warning "Failed to obtain management access token; attempting az rest as fallback"
    $uri = "https://management.chinacloudapi.cn/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.Web/sites/$webAppName/config/appsettings?api-version=2022-03-01"
    az rest --method PUT --uri $uri --body $jsonBody --headers @{"Content-Type"="application/json"} | Out-Null
} else {
    $uri = "https://management.chinacloudapi.cn/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroup/providers/Microsoft.Web/sites/$webAppName/config/appsettings?api-version=2022-03-01"
    Invoke-RestMethod -Method Put -Uri $uri -Headers @{ Authorization = "Bearer $token"; "Content-Type" = "application/json" } -Body $jsonBody | Out-Null
}

Write-Host "App settings configured" -ForegroundColor Green

# Step 10: Build and Deploy
Write-Host "Building application..." -ForegroundColor Yellow

if (Test-Path "publish-staging") { Remove-Item -Recurse -Force "publish-staging" }
if (Test-Path "deploy-staging.zip") { Remove-Item -Force "deploy-staging.zip" }

$buildOutput = dotnet publish src/FileService.Api/FileService.Api.csproj -c Release -o publish-staging 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed: $buildOutput"
    exit 1
}

Write-Host "Build completed" -ForegroundColor Green

Write-Host "Creating deployment package..." -ForegroundColor Yellow
Push-Location publish-staging
Compress-Archive -Path * -DestinationPath ../deploy-staging.zip -Force
Pop-Location

Write-Host "Deploying to Azure..." -ForegroundColor Yellow
az webapp deploy --resource-group $ResourceGroup --name $webAppName --src-path deploy-staging.zip --type zip | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Host "Deployment completed successfully!" -ForegroundColor Green
} else {
    Write-Warning "Deployment may have issues. Check web app logs."
}

# Cleanup
Remove-Item -Recurse -Force publish-staging -ErrorAction SilentlyContinue
Remove-Item -Force deploy-staging.zip -ErrorAction SilentlyContinue

# Step 11: Configure Custom Domain
if ($CustomDomain) {
    $fullHostname = "filesvc-stg-app.$CustomDomain"
    Write-Host "Adding custom domain: $fullHostname" -ForegroundColor Yellow
    az webapp config hostname add --resource-group $ResourceGroup --webapp-name $webAppName --hostname $fullHostname | Out-Null
    Write-Host "Custom domain added" -ForegroundColor Green
}

# Step 12: Configure SSL Certificate
if ($CertificateFile -and $CertificatePassword) {
    Write-Host "Uploading provided SSL certificate..." -ForegroundColor Yellow
    $thumbprint = az webapp config ssl upload --resource-group $ResourceGroup --name $webAppName --certificate-file $CertificateFile --certificate-password $CertificatePassword --query thumbprint -o tsv
    Write-Host "Binding SSL certificate..." -ForegroundColor Yellow
    $fullHostname = "filesvc-stg-app.$CustomDomain"
    az webapp config ssl bind --resource-group $ResourceGroup --name $webAppName --certificate-thumbprint $thumbprint --ssl-type SNI --hostname $fullHostname | Out-Null
    Write-Host "SSL certificate bound" -ForegroundColor Green
} elseif ($UseLetsEncrypt -and $CustomDomain) {
    $fullHostname = "filesvc-stg-app.$CustomDomain"
    Write-Host "Obtaining Let's Encrypt SSL certificate for $fullHostname..." -ForegroundColor Yellow
    # Install certbot if not present
    if (-not (Get-Command certbot -ErrorAction SilentlyContinue)) {
        Write-Host "Installing certbot..." -ForegroundColor Yellow
        python -m pip install certbot
    }
    # Run certbot for DNS challenge
    Write-Host "Running certbot for DNS challenge. You may need to add a TXT record to your DNS." -ForegroundColor Yellow
    certbot certonly --manual --preferred-challenges dns -d $fullHostname --agree-tos --email "admin@$CustomDomain" --no-eff-email
    # Assume cert is in /etc/letsencrypt/live/$fullHostname/
    $certDir = "/etc/letsencrypt/live/$fullHostname"
    if (Test-Path $certDir) {
        $certPath = "$env:TEMP\$CustomDomain.pfx"
        # Convert to PFX using PowerShell
        $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2
        $cert.Import("$certDir\fullchain.pem", $null, [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::Exportable)
        $key = [System.IO.File]::ReadAllText("$certDir\privkey.pem")
        # Note: Full conversion requires more steps; for simplicity, assume user provides PFX
        Write-Host "Certificate obtained. Please convert to .pfx manually and provide via -CertificateFile." -ForegroundColor Yellow
    } else {
        Write-Host "Certificate not found. Please complete the DNS challenge manually." -ForegroundColor Red
    }
} elseif ($CustomDomain) {
    $fullHostname = "filesvc-stg-app.$CustomDomain"
    Write-Host "Generating self-signed SSL certificate for $fullHostname..." -ForegroundColor Yellow
    $certName = "ssl-cert-$fullHostname".Replace(".", "-")
    $certPassword = "P@ssword123!"
    $certPath = "$env:TEMP\$certName.pfx"
    # Generate self-signed certificate
    $cert = New-SelfSignedCertificate -DnsName $fullHostname -CertStoreLocation "cert:\CurrentUser\My" -KeyExportPolicy Exportable -Provider "Microsoft Enhanced RSA and AES Cryptographic Provider"
    # Export to PFX with password
    $securePassword = ConvertTo-SecureString -String $certPassword -Force -AsPlainText
    Export-PfxCertificate -Cert $cert -FilePath $certPath -Password $securePassword
    # Remove from store
    Remove-Item -Path "cert:\CurrentUser\My\$($cert.Thumbprint)" -Force
    Write-Host "Uploading certificate to App Service..." -ForegroundColor Yellow
    $thumbprint = az webapp config ssl upload --resource-group $ResourceGroup --name $webAppName --certificate-file $certPath --certificate-password $certPassword --query thumbprint -o tsv
    Write-Host "Binding SSL certificate..." -ForegroundColor Yellow
    az webapp config ssl bind --resource-group $ResourceGroup --name $webAppName --certificate-thumbprint $thumbprint --ssl-type SNI --hostname $fullHostname | Out-Null
    Remove-Item $certPath -Force
    Write-Host "Self-signed SSL certificate generated, uploaded, and bound (Note: Self-signed certs are not trusted by browsers. Use a proper CA cert for production)." -ForegroundColor Green
}

Write-Host ""
Write-Host "=== Deployment Complete ===" -ForegroundColor Cyan
Write-Host "Web App URL: https://$webAppName.chinacloudsites.cn" -ForegroundColor White
if ($CustomDomain) {
    $fullHostname = "filesvc-stg-app.$CustomDomain"
    Write-Host "Custom Domain URL: https://$fullHostname" -ForegroundColor White
}
Write-Host "Swagger URL: https://$webAppName.chinacloudsites.cn/swagger" -ForegroundColor White
Write-Host ""
Write-Host "Configuration:" -ForegroundColor Yellow
Write-Host "- Using Azure Table Storage for metadata" -ForegroundColor White  
Write-Host "- Using Azure Blob Storage for files" -ForegroundColor White
Write-Host "- Managed Identity authentication" -ForegroundColor White
Write-Host ""
Write-Host "To monitor logs:" -ForegroundColor Yellow
Write-Host "az webapp log tail --name $webAppName --resource-group $ResourceGroup" -ForegroundColor White