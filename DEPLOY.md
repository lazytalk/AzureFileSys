# Deployment Guide

This document explains how to deploy the File Service across three environments: Development (local), Staging, and Production. It covers local dev, Azure multi-environment provisioning, configuration management, and CI/CD pipeline with proper promotion workflows.

---
## 1. Environment Overview

The service supports three deployment environments:

### **🔧 Development (Local)**
- **Purpose**: Local development and testing
- **Metadata Storage**: In-Memory Repository (data resets on restart)
- **Blob Storage**: Stub implementation (no Azure dependencies)
- **Auth**: Development bypass (`?devUser=alice`)
- **URL**: `http://localhost:5090`

### **🧪 Staging**
- **Purpose**: Pre-production testing, integration testing, UAT
- **Metadata Storage**: Azure Table Storage (shared with blob storage account)
- **Blob Storage**: Azure Blob Storage (separate container)
- **Auth**: Real PowerSchool integration (test instance)
- **URL**: `https://filesvc-api-staging.azurewebsites.net`

### **🚀 Production**
- **Purpose**: Live production workloads
- **Metadata Storage**: Azure Table Storage (shared with blob storage account)
- **Blob Storage**: Azure Blob Storage (production container)
- **Auth**: Real PowerSchool integration (production instance)
- **URL**: `https://filesvc-api-prod.azurewebsites.net`

---
## 2. Environment Configuration Matrix

| Setting | Development | Staging | Production |
|---------|-------------|---------|------------|
| `ASPNETCORE_ENVIRONMENT` | Development | Staging | Production |
| `BlobStorage__UseLocalStub` | true | false | false |
| `BlobStorage__ConnectionString` | (empty) | Key Vault Reference | Key Vault Reference |
| `BlobStorage__ContainerName` | userfiles | userfiles-staging | userfiles-prod |
| `Persistence__Type` | InMemory | TableStorage | TableStorage |
| `TableStorage__ConnectionString` | (empty) | Key Vault Reference | Key Vault Reference |
| `TableStorage__TableName` | FileMetadata | FileMetadata | FileMetadata |
| **Storage Account SKU** | N/A | Standard_LRS | Standard_GRS |
| **App Service SKU** | N/A | B1 (Basic) | P1v2 (Premium) |
| **Monitoring Level** | Console only | Basic alerts | Full monitoring |
| **Backup Retention** | N/A | 7 days | 30 days |

> **Note**: Key Vault References use format: `@Microsoft.KeyVault(VaultName=<vault>;SecretName=<secret>)`

---
## 3. Quick Start - Unified Deployment

The unified `deploy.ps1` script handles both staging and production deployments:

### **Deploy to Staging**
```powershell
cd scripts
.\deploy.ps1 -Environment Staging -CreateResources -DeployApp
```

### **Deploy to Production** 
```powershell
cd scripts
# Option 1: Rebuild from source
.\deploy.ps1 -Environment Production -CreateResources -DeployApp

# Option 2: Promote tested staging build (recommended)
.\deploy.ps1 -Environment Production -DeployApp -PromoteFromStaging
```

### **Parameters**
- `-Environment` (required): `Staging` or `Production`
- `-CreateResources`: Create all Azure resources (resource group, Storage Account, Key Vault, App Service)
- `-DeployApp`: Build and deploy the application
- `-PromoteFromStaging` (production only): Use staging build instead of rebuilding
- `-Location`: Override deployment region (default: from config)
- `-SubscriptionId`: Set Azure subscription

> **Note**: Table Storage tables are automatically created on first use - no manual schema management required

---
## 4. Multi-Environment Azure Architecture

```
Azure Subscription
├── 📂 KWE-RescourceGroup-ChinaNorth3-Staging-FileSystem          # Staging Resource Group
│   ├── 🌐 filesvc-api-staging      # Staging Web App
│   ├── � filesvcstgstaging123     # Staging Storage Account (Blob + Table)
│   ├── 🔐 filesvc-kv-staging       # Staging Key Vault
│   └── 📊 filesvc-ai-staging       # Staging App Insights
│
└── 📂 KWE-RescourceGroup-ChinaNorth3-Production-FileSystem       # Production Resource Group
    ├── 🌐 filesvc-api-prod         # Production Web App
    ├── 💾 filesvcstgprod456        # Production Storage Account (Blob + Table)
    ├── 🔐 filesvc-kv-prod          # Production Key Vault
    └── 📊 filesvc-ai-prod          # Production App Insights
```

---
## 5. Configuration Keys Reference

| Env Var / Key | Purpose | Dev Default | Staging | Production |
|---------------|---------|-------------|---------|------------|
| `ASPNETCORE_ENVIRONMENT` | ASP.NET environment mode | Development | Staging | Production |
| `BlobStorage__UseLocalStub` | Use in-memory file bytes | true | false | false |
| `BlobStorage__ConnectionString` | Azure Storage connection | (empty) | Key Vault Ref | Key Vault Ref |
| `BlobStorage__ContainerName` | Container for user files | userfiles | userfiles-staging | userfiles-prod |
| `Persistence__Type` | Persistence implementation | InMemory | TableStorage | TableStorage |
| `TableStorage__ConnectionString` | Table storage connection | (empty) | Key Vault Ref | Key Vault Ref |
| `TableStorage__TableName` | Table name for metadata | FileMetadata | FileMetadata | FileMetadata |
| `ApplicationInsights__InstrumentationKey` | AI monitoring | (unused) | Key Vault Ref | Key Vault Ref |
| `PowerSchool__BaseUrl` | PowerSchool API endpoint | (bypass) | test-ps.school.edu | ps.school.edu |
| `PowerSchool__ApiKey` | PowerSchool authentication | (bypass) | Key Vault Ref | Key Vault Ref |

> **Note**: Double underscore (`__`) maps to nested JSON keys. Key Vault Ref = `@Microsoft.KeyVault(...)`

---
## 6. Local Development (No Changes)

1. Run dev script (installs .NET 9 SDK if needed):
   ```powershell
   cd scripts
   ./dev-run.ps1 -Port 5090
   ```
   > **Note**: Development mode uses in-memory storage (data resets on restart)

2. Upload test:
   ```powershell
   Invoke-RestMethod -Method Post -Uri 'http://localhost:5090/api/files/upload?devUser=demo1' -Form @{ file=Get-Item ..\README.md }
   ```
3. List:
   ```powershell
   Invoke-RestMethod -Method Get -Uri 'http://localhost:5090/api/files?devUser=demo1'
   ```

---
## 7. Complete Staging Deployment

```powershell
# 1. Create all Azure resources
.\deploy.ps1 -Environment Staging -CreateResources

# 2. Deploy application
.\deploy.ps1 -Environment Staging -DeployApp

# OR: Do all at once
.\deploy.ps1 -Environment Staging -CreateResources -DeployApp
```

> **Note**: Table Storage tables are automatically created on first API call - no manual schema management required

---
## 8. Production Deployment Workflow

### **Option A: Promote Tested Staging Build (Recommended)**
```powershell
# 1. Deploy to staging first
.\deploy.ps1 -Environment Staging -CreateResources -DeployApp

# 2. Test staging thoroughly...

# 3. Create production resources
.\deploy.ps1 -Environment Production -CreateResources

# 4. Promote staging build to production
.\deploy.ps1 -Environment Production -DeployApp -PromoteFromStaging
```

**Benefits:**
- ✅ Same code tested in staging goes to production
- ✅ Fast deployment (no rebuild)
- ✅ Reduces risk of production-specific build issues
- ✅ Follows industry best practices
- ✅ No database migrations needed (Table Storage auto-creates tables)

### **Option B: Fresh Build for Production**
```powershell
.\deploy.ps1 -Environment Production -CreateResources -DeployApp
```

---
## 9. Deployment Operations


Run the production deployment script to create resources:

```powershell
.\scripts\deploy-production.ps1 -CreateResources
```

See `scripts/deploy-production.ps1` for the full script implementation.

---
## 8. Deployment Automation Scripts

Create these PowerShell scripts in your `scripts/` folder:

### 8.1 `deploy-staging.ps1`

See `scripts/deploy-staging.ps1` for the full script.

### 8.2 `deploy-production.ps1`

See `scripts/deploy-production.ps1` for the full script.

---
## 9. CI/CD Pipeline for Multi-Environment

For automated deployment, use the scripts in `scripts/` folder. The pipeline should include:

- Resource creation (using `-CreateResources`)
- App deployment (using `-DeployApp`)

See the unified `deploy.ps1` script for implementation details.

> **Note**: Table Storage tables are automatically created on first use - no schema migration needed

---


## 10. Table Storage Setup

### Table Storage Architecture
1. **No Schema Management Required**:
   - Tables are automatically created on first use
   - No migrations or schema updates needed
   - Schema is defined in code (FileRecordEntity)

2. **Table Design**:
   ```csharp
   // PartitionKey: OwnerUserId (enables efficient user-scoped queries)
   // RowKey: FileId (GUID, ensures uniqueness)
   // Properties: FileName, ContentType, SizeBytes, UploadedAt, BlobPath
   ```

3. **Connection Configuration**:
   ```json
   {
     "TableStorage": {
       "ConnectionString": "@Microsoft.KeyVault(VaultName=filesvc-kv-prod;SecretName=TableStorage--ConnectionString)",
       "TableName": "FileMetadata"
     }
   }
   ```

### Benefits Over SQL Database:
- ✅ **No schema migrations** - table structure defined in code
- ✅ **Automatic scaling** - serverless NoSQL storage
- ✅ **Lower cost** - pay per transaction instead of fixed DTU
- ✅ **Shared storage account** - uses same account as blob storage
- ✅ **High availability** - built-in replication and redundancy

## 11. Infrastructure as Code (Optional Enhancement)

Create `azure-resources.bicep` for reproducible deployments:

```bicep
param location string = resourceGroup().location
param appName string

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-04-01' = {
  name: '${appName}stg${uniqueString(resourceGroup().id)}'
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    // Supports both Blob Storage and Table Storage
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
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
      netFrameworkVersion: 'v9.0'
      metadata: [{ name: 'CURRENT_STACK', value: 'dotnet' }]
    }
  }
  identity: { type: 'SystemAssigned' }
}

resource keyVault 'Microsoft.KeyVault/vaults@2021-06-01-preview' = {
  name: '${appName}-kv'
  location: location
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
  }
}
```

Deploy with:
```powershell
az deployment group create -g filesvc-rg --template-file azure-resources.bicep --parameters appName=filesvc-api
```

## 12. Production Deployment Checklist

### Pre-Deployment
- [ ] Update `appsettings.Production.json` with production settings
- [ ] Test application locally with production-like configuration
- [ ] Run all unit tests: `dotnet test`
- [ ] Run smoke tests against staging environment
- [ ] Verify all secrets are stored in Key Vault
- [ ] Review security settings and access controls

### Deployment
- [ ] Create all Azure resources using the deployment script
- [ ] Build and deploy application: `dotnet publish` + `az webapp deploy`
- [ ] Verify Table Storage table is auto-created on first API call
- [ ] Verify managed identity permissions for Storage Account
- [ ] Test Key Vault secret access

### Post-Deployment
- [ ] Verify `/swagger` endpoint is accessible (Staging only)
- [ ] Test file upload via Swagger UI or API client
- [ ] Verify files are stored in Azure Blob Storage
- [ ] Verify metadata is stored in Azure Table Storage
- [ ] Test file download and delete operations
- [ ] Monitor Application Insights for errors
- [ ] Set up alerts for critical metrics
- [ ] Configure backup policies for Storage Account

### PowerSchool Integration (Production)
- [ ] Replace dev authentication bypass with real PowerSchool integration
- [ ] Implement proper HMAC token validation
- [ ] Configure PowerSchool server endpoints and credentials
- [ ] Test user authentication and role-based access
- [ ] Set up audit logging for authentication events

## 13. Monitoring & Maintenance

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

### Table Storage Monitoring
```kusto
// Monitor table storage operations in Application Insights
requests
| where cloud_RoleName == "FileService.Api"
| where url contains "files"
| summarize 
    uploads = countif(name == "POST /api/files/upload"),
    lists = countif(name == "GET /api/files"),
    deletes = countif(name == "DELETE /api/files/{id}")
    by bin(timestamp, 1h)

// Track file metadata by user
customEvents
| where name in ("FileUploaded", "FileDeleted")
| extend userId = tostring(customDimensions.UserId)
| summarize 
    fileCount = count(),
    totalSizeBytes = sum(tolong(customDimensions.FileSizeBytes))
    by userId
```

```powershell
# Query Table Storage directly using Azure CLI
az storage entity query \
  --account-name <storage-account> \
  --table-name FileMetadata \
  --filter "PartitionKey eq 'user123'"

# Get table statistics
az storage table stats \
  --account-name <storage-account> \
  --table-name FileMetadata
```

## 14. Backup & Disaster Recovery

### Storage Account Backups (Blob + Table)
```powershell
# Enable soft delete for blobs (30-day retention)
az storage account blob-service-properties update \
  --account-name <storage-account> \
  --enable-delete-retention true \
  --delete-retention-days 30

# Enable versioning for blobs
az storage account blob-service-properties update \
  --account-name <storage-account> \
  --enable-versioning true

# Enable point-in-time restore for containers
az storage account blob-service-properties update \
  --account-name <storage-account> \
  --enable-restore-policy true \
  --restore-days 7
```

### Table Storage Data Protection
```powershell
# Export table data for backup (using Azure Storage Explorer or custom script)
# Table Storage supports geo-replication with GRS/RA-GRS storage accounts

# Configure geo-redundant storage for production
az storage account update \
  --name <storage-account> \
  --sku Standard_GRS  # or Standard_RAGRS for read-access geo-redundancy
```

### Disaster Recovery Strategy
- ✅ **GRS Replication**: Automatically replicates data to secondary region
- ✅ **Soft Delete**: Protects against accidental blob deletion (30-day retention)
- ✅ **Versioning**: Maintains historical versions of blobs
- ✅ **Point-in-Time Restore**: Restore containers to previous state (7-day window)
- ✅ **Table Backup**: Export critical table data periodically using Azure Storage Explorer

---
## 15. Summary

The deployment guide now provides:

✅ **Complete Azure Resource Provisioning** - All necessary services  
✅ **Security Best Practices** - Managed Identity, Key Vault, HTTPS  
✅ **Serverless Metadata Storage** - Azure Table Storage (no schema migrations)  
✅ **Monitoring & Alerting** - Application Insights with custom metrics  
✅ **Infrastructure as Code** - Bicep templates for reproducible deployments  
✅ **Comprehensive Checklist** - Pre/post deployment validation  
✅ **Backup & Recovery** - Storage account protection with geo-redundancy  

This guide ensures a production-ready deployment with enterprise-grade security, monitoring, and reliability! 🚀

## 16. Troubleshooting Multi-Environment Issues

| Environment | Symptom | Possible Cause | Fix |
|-------------|---------|----------------|-----|
| **Staging** | 401 responses | Missing auth headers | Use real PowerSchool headers or check staging credentials |
| **Staging** | Upload works but download 404 | Wrong container name | Verify `userfiles-staging` container exists |
| **Staging** | Metadata not saving | Table Storage access issue | Check managed identity permissions on Storage Account |
| **Production** | Swagger disabled | Production config | Expected behavior - use staging for API testing |
| **Production** | High memory usage | Large file processing | Monitor App Insights and consider scaling up |
| **Both** | Key Vault secret not found | Wrong secret name format | Use exact format: `TableStorage--ConnectionString` |
| **Both** | Table not created | First API call failed | Check Application Insights for initialization errors |

### Common Resolution Steps:

#### 1. Key Vault Access Issues
```powershell
# Check managed identity permissions
az webapp identity show -n <webapp-name> -g <resource-group>
az keyvault show -n <keyvault-name> -g <resource-group> --query properties.accessPolicies
```

#### 2. Table Storage Access Problems
```powershell
# Test Table Storage connection
az storage table exists \
  --account-name <storage-account> \
  --name FileMetadata

# Check managed identity permissions
az role assignment list \
  --assignee <managed-identity-principal-id> \
  --scope /subscriptions/<sub-id>/resourceGroups/<rg>/providers/Microsoft.Storage/storageAccounts/<storage-account>

# Verify table exists and has data
az storage entity query \
  --account-name <storage-account> \
  --table-name FileMetadata \
  --select RowKey,PartitionKey
```

#### 3. Storage Access Issues
```powershell
# Check storage container and permissions
az storage container show --account-name <storage-account> -n <container-name>
az role assignment list --assignee <managed-identity-principal-id> --scope <storage-account-resource-id>
```

---
## 17. Environment Management Best Practices

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
## 18. Quick Start Commands

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
## 19. Cost Estimation

### Monthly Azure Costs (Estimate)

| Service | Staging | Production | Notes |
|---------|---------|------------|-------|
| **App Service** | $13 (B1) | $73 (P1v2) | Staging can auto-scale down |
| **Storage Account** | $3 | $8 | Includes Blob + Table Storage with GRS |
| **Application Insights** | $0-5 | $10-20 | Based on telemetry volume |
| **Key Vault** | $1 | $1 | Secret operations |
| **Total/Month** | **~$17** | **~$102** | Approximate costs |

### Cost Savings vs SQL Server:
- ✅ **Staging**: Save ~$5/month (no SQL Database)
- ✅ **Production**: Save ~$30/month (no SQL Database)
- ✅ **Pay per transaction** instead of fixed DTU costs
- ✅ **No database tier upgrades** needed for scaling

### Cost Optimization Tips:
- 🔄 **Auto-shutdown staging** during non-business hours
- 🔄 **Scale down staging** App Service to B1 or even F1 (free tier)
- 🔄 **Use LRS storage** for staging (lower redundancy)
- 🔄 **Monitor usage** with Azure Cost Management
- 🔄 **Table Storage pricing** is transaction-based (~$0.10 per 100K transactions)

---
## 20. Conclusion

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
4. **Set up your PowerSchool integration** with real credentials
5. **Train your team** on the promotion workflow

This architecture provides **enterprise-grade multi-environment deployment** with proper isolation, security, and operational practices! 🌟
