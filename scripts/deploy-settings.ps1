# config.ps1 - Unified configuration for all environments (application settings + Azure resources)
# This is the single source of truth for all deployment configuration

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("Development", "Staging", "Production")]
    [string]$Environment
)

$ErrorActionPreference = 'Stop'

# ============================================================================
# SECTION 1: ENVIRONMENT-SPECIFIC CONFIGURATION
# ============================================================================

$envConfig = switch ($Environment) {
    "Development" {
        @{
            "EnvSuffix" = "dev"
            "EnvLabel" = "dev"
            "Location" = "chinanorth3"
            "SubscriptionId" = ""
            "ResourceGroup" = "KWE-RescourceGroup-ChinaNorth3-Development-FileSystem"
            "AppServiceSku" = "F1"
            "AppServicePlanName" = "filesvc-dev-plan"
            "PowerSchoolBaseUrl" = "https://dev-powerschool.school.edu"
            "CertificatePassword" = ""
            # Application settings
            "ASPNETCORE_ENVIRONMENT" = "Development"
            "BlobStorage__UseLocalStub" = "true"
            "BlobStorage__ContainerName" = "userfiles"
            "Persistence__Type" = "InMemory"
        }
    }
    "Staging" {
        @{
            "EnvSuffix" = "stg"
            "EnvLabel" = "staging"
            "Location" = "chinanorth3"
            "SubscriptionId" = ""
            "ResourceGroup" = "KWE-RescourceGroup-ChinaNorth3-Staging-FileSystem"
            "AppServiceSku" = "B1"
            "AppServicePlanName" = "filesvc-staging-plan"
            "PowerSchoolBaseUrl" = "https://ps1.kaiwenacademy.cn"
            "CustomDomain" = "filesvc-stg-app.kaiweneducation.com"
            "CertificatePfxFileName" = "filesvc-stg-app.kaiweneducation.com.pfx"
            "CertificatePassword" = "5fuutvuc"
            "CorsAllowedOrigins" = "https://ps1.kaiwenacademy.cn;https://filesvc-stg-app.kaiweneducation.com;http://localhost:3000;http://localhost:5090;http://localhost:8080;http://localhost:5173"
            "TableName" = "FileMetadata"
            # Application settings
            "ASPNETCORE_ENVIRONMENT" = "Staging"
            "BlobStorage__UseLocalStub" = "false"
            "BlobStorage__ContainerName" = "userfiles-staging"
            "Persistence__Type" = "TableStorage"
            "TableStorage__TableName" = "FileMetadata"
        }
    }
    "Production" {
        @{
            "EnvSuffix" = "prd"
            "EnvLabel" = "prod"
            "Location" = "chinanorth3"
            "SubscriptionId" = ""
            "ResourceGroup" = "KWE-RescourceGroup-ChinaNorth3-Production-FileSystem"
            "AppServiceSku" = "P1v2"
            "AppServicePlanName" = "filesvc-prod-plan"
            "PowerSchoolBaseUrl" = "https://ps.kaiwenacademy.com"
            "CustomDomain" = "filesvc.kaiweneducation.com"
            "CertificatePfxFileName" = "filesvc.kaiweneducation.com.pfx"
            "CertificatePassword" = ""
            "CorsAllowedOrigins" = "https://ps1.kaiwenacademy.cn;https://filesvc.kaiweneducation.com"
            "TableName" = "FileMetadata"
            # Application settings
            "ASPNETCORE_ENVIRONMENT" = "Production"
            "BlobStorage__UseLocalStub" = "false"
            "BlobStorage__ContainerName" = "userfiles-prod"
            "Persistence__Type" = "TableStorage"
            "TableStorage__TableName" = "FileMetadata"
        }
    }
}

# ============================================================================
# SECTION 2: APPLY COMMAND-LINE OVERRIDES
# ============================================================================

# (No overrides needed for Table Storage)

# ============================================================================
# SECTION 3: BUILD RESOURCE NAMES
# ============================================================================

# Use company abbreviation as suffix for global uniqueness
$suffix = "kwe"

$resources = @{
    "Environment" = $Environment
    "EnvSuffix" = $envConfig["EnvSuffix"]
    "EnvLabel" = $envConfig["EnvLabel"]
    "Location" = $envConfig["Location"]
    "SubscriptionId" = $envConfig["SubscriptionId"]
    "ResourceGroup" = $envConfig["ResourceGroup"]
    "AppServiceSku" = $envConfig["AppServiceSku"]
    "AppServicePlanName" = $envConfig["AppServicePlanName"] + "-$suffix"
    "PowerSchoolBaseUrl" = $envConfig["PowerSchoolBaseUrl"]
    "CustomDomain" = $envConfig["CustomDomain"]
    "StorageAccount" = "filesvc$($envConfig["EnvSuffix"])$suffix"
    "WebAppName" = "filesvc-$($envConfig["EnvSuffix"])-app"
    "KeyVaultName" = "kv-fsvc-$($envConfig["EnvSuffix"])-$suffix"
    "AppInsightsName" = "filesvc-ai-$($envConfig["EnvLabel"])"
    "TableName" = $envConfig["TableName"]
    "CertificatePfxFileName" = $envConfig["CertificatePfxFileName"]
    "CertificatePassword" = $envConfig["CertificatePassword"]
}

# ============================================================================
# SECTION 4: BUILD APPLICATION SETTINGS
# ============================================================================

# Extract application settings from environment config
$appSettings = @{
    "ASPNETCORE_ENVIRONMENT" = $envConfig["ASPNETCORE_ENVIRONMENT"]
    "BlobStorage__UseLocalStub" = $envConfig["BlobStorage__UseLocalStub"]
    "BlobStorage__ContainerName" = $envConfig["BlobStorage__ContainerName"]
    "Persistence__Type" = $envConfig["Persistence__Type"]
    "TableStorage__TableName" = $envConfig["TableStorage__TableName"]
    "Cors__AllowedOrigins" = $envConfig["CorsAllowedOrigins"]
}

# ============================================================================
# SECTION 5: RETURN CONFIGURATION
# ============================================================================

# Return both resources and appSettings for calling script to use
return @{
    Resources = $resources
    AppSettings = $appSettings
}
