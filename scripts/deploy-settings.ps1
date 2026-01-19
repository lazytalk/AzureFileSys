# config.ps1 - Unified configuration for all environments (application settings + Azure resources)
# This is the single source of truth for all deployment configuration

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("Development", "Staging", "Production")]
    [string]$Environment,
    
    [string]$SqlAdminPassword = ""  # Optional - overrides config default if provided
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
            "ResourceGroup" = "KWE-RescourceGroup-ChinaNorth3-Development-FileSystem"
            "SqlTier" = "Free"
            "AppServiceSku" = "F1"
            "AppServicePlanName" = "filesvc-dev-plan"
            "PowerSchoolBaseUrl" = "https://dev-powerschool.school.edu"
            "SqlAdminPassword" = "DevPass123!"
            # Application settings
            "ASPNETCORE_ENVIRONMENT" = "Development"
            "BlobStorage__UseLocalStub" = "true"
            "BlobStorage__ContainerName" = "userfiles"
            "Persistence__UseEf" = "true"
            "Persistence__UseSqlServer" = "false"
        }
    }
    "Staging" {
        @{
            "EnvSuffix" = "stg"
            "EnvLabel" = "staging"
            "Location" = "chinanorth3"
            "ResourceGroup" = "KWE-RescourceGroup-ChinaNorth3-Staging-FileSystem"
            "SqlTier" = "Basic"
            "AppServiceSku" = "B1"
            "AppServicePlanName" = "filesvc-staging-plan"
            "PowerSchoolBaseUrl" = "https://test-powerschool.school.edu"
            "SqlAdminPassword" = "StagingSecurePass123!"
            # Application settings
            "ASPNETCORE_ENVIRONMENT" = "Staging"
            "BlobStorage__UseLocalStub" = "false"
            "BlobStorage__ContainerName" = "userfiles-staging"
            "Persistence__UseEf" = "true"
            "Persistence__UseSqlServer" = "true"
        }
    }
    "Production" {
        @{
            "EnvSuffix" = "prd"
            "EnvLabel" = "prod"
            "Location" = "chinanorth3"
            "ResourceGroup" = "KWE-RescourceGroup-ChinaNorth3-Production-FileSystem"
            "SqlTier" = "Standard"
            "AppServiceSku" = "P1v2"
            "AppServicePlanName" = "filesvc-prod-plan"
            "PowerSchoolBaseUrl" = "https://ps.school.edu"
            "SqlAdminPassword" = "ProductionSecurePass456!"
            # Application settings
            "ASPNETCORE_ENVIRONMENT" = "Production"
            "BlobStorage__UseLocalStub" = "false"
            "BlobStorage__ContainerName" = "userfiles-prod"
            "Persistence__UseEf" = "true"
            "Persistence__UseSqlServer" = "true"
        }
    }
}

# ============================================================================
# SECTION 2: APPLY COMMAND-LINE OVERRIDES
# ============================================================================

# Use provided password from command-line, or fall back to environment default
if ([string]::IsNullOrWhiteSpace($SqlAdminPassword)) {
    $SqlAdminPassword = $envConfig["SqlAdminPassword"]
} else {
    Write-Host "Using SQL admin password provided via command-line parameter" -ForegroundColor Green
}

# ============================================================================
# SECTION 3: BUILD RESOURCE NAMES
# ============================================================================

$resources = @{
    "Environment" = $Environment
    "EnvSuffix" = $envConfig["EnvSuffix"]
    "EnvLabel" = $envConfig["EnvLabel"]
    "Location" = $envConfig["Location"]
    "ResourceGroup" = $envConfig["ResourceGroup"]
    "SqlTier" = $envConfig["SqlTier"]
    "AppServiceSku" = $envConfig["AppServiceSku"]
    "AppServicePlanName" = $envConfig["AppServicePlanName"]
    "PowerSchoolBaseUrl" = $envConfig["PowerSchoolBaseUrl"]
    "StorageAccount" = "filesvc$($envConfig["EnvSuffix"])$(Get-Random -Minimum 1000 -Maximum 9999)"
    "WebAppName" = "filesvc-api-$($envConfig["EnvLabel"])"
    "KeyVaultName" = "filesvc-kv-$($envConfig["EnvLabel"])"
    "AppInsightsName" = "filesvc-ai-$($envConfig["EnvLabel"])"
    "SqlServerName" = "filesvc-sql-$($envConfig["EnvLabel"])"
    "SqlDbName" = "file-service-db"
    "SqlAdminUser" = "fsadmin"
    "SqlAdminPassword" = $SqlAdminPassword
}

# ============================================================================
# SECTION 4: BUILD APPLICATION SETTINGS
# ============================================================================

# Extract application settings from environment config
$appSettings = @{
    "ASPNETCORE_ENVIRONMENT" = $envConfig["ASPNETCORE_ENVIRONMENT"]
    "BlobStorage__UseLocalStub" = $envConfig["BlobStorage__UseLocalStub"]
    "BlobStorage__ContainerName" = $envConfig["BlobStorage__ContainerName"]
    "Persistence__UseEf" = $envConfig["Persistence__UseEf"]
    "Persistence__UseSqlServer" = $envConfig["Persistence__UseSqlServer"]
}

# ============================================================================
# SECTION 5: RETURN CONFIGURATION
# ============================================================================

# Return both resources and appSettings for calling script to use
return @{
    Resources = $resources
    AppSettings = $appSettings
}
