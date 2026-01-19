# Deployment Guide

This document explains how to deploy the File Service across three environments: Development (local), Staging, and Production. It covers local dev, Azure multi-environment provisioning, configuration management, and CI/CD pipeline with proper promotion workflows.

---
## 1. Environment Overview

The service supports three deployment environments:

### **🔧 Development (Local)**
- **Purpose**: Local development and testing
- **Database**: SQLite or In-Memory Repository
- **Storage**: Stub implementation (no Azure dependencies)
- **Auth**: Development bypass (`?devUser=alice`)
- **URL**: `http://localhost:5090`

### **🧪 Staging**
- **Purpose**: Pre-production testing, integration testing, UAT
- **Database**: Azure SQL Database (smaller tier)
- **Storage**: Azure Blob Storage (separate container)
- **Auth**: Real PowerSchool integration (test instance)
- **URL**: `https://filesvc-api-staging.azurewebsites.net`

### **🚀 Production**
- **Purpose**: Live production workloads
- **Database**: Azure SQL Database (production tier)
- **Storage**: Azure Blob Storage (production container)
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
| `Persistence__UseEf` | false (in-memory) | true | true |
| `Persistence__UseSqlServer` | false | true | true |
| `Sql__ConnectionString` | (unused) | Key Vault Reference | Key Vault Reference |
| **Database SKU** | N/A | Basic (5 DTU) | Standard S2 (50 DTU) |
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
.\deploy.ps1 -Environment Staging -CreateResources -DeployApp -RunMigrations
```

### **Deploy to Production** 
```powershell
cd scripts
# Option 1: Rebuild from source
.\deploy.ps1 -Environment Production -CreateResources -DeployApp -RunMigrations

# Option 2: Promote tested staging build (recommended)
.\deploy.ps1 -Environment Production -DeployApp -RunMigrations -PromoteFromStaging
```

### **Parameters**
- `-Environment` (required): `Staging` or `Production`
- `-CreateResources`: Create all Azure resources (resource group, SQL DB, Storage, Key Vault, App Service)
- `-DeployApp`: Build and deploy the application
- `-RunMigrations`: Run Entity Framework migrations
- `-PromoteFromStaging` (production only): Use staging build instead of rebuilding
- `-Location`: Override deployment region (default: from config)
- `-SqlAdminPassword`: Override SQL admin password (default: from config)
- `-SubscriptionId`: Set Azure subscription

---
## 4. Multi-Environment Azure Architecture

```
Azure Subscription
├── 📂 KWE-RescourceGroup-ChinaNorth3-Staging-FileSystem          # Staging Resource Group
│   ├── 🌐 filesvc-api-staging      # Staging Web App
│   ├── 🗄️ filesvc-sql-staging      # Staging SQL Server
│   ├── 💾 filesvcstgstaging123     # Staging Storage Account
│   ├── 🔐 filesvc-kv-staging       # Staging Key Vault
│   └── 📊 filesvc-ai-staging       # Staging App Insights
│
└── 📂 KWE-RescourceGroup-ChinaNorth3-Production-FileSystem       # Production Resource Group
    ├── 🌐 filesvc-api-prod         # Production Web App
    ├── 🗄️ filesvc-sql-prod         # Production SQL Server
    ├── 💾 filesvcstgprod456        # Production Storage Account
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
| `Persistence__UseEf` | EF Core enabled | false | true | true |
| `Persistence__UseSqlServer` | Use SQL Server instead of SQLite | false | true | true |
| `Sql__ConnectionString` | SQL Server connection | (unused) | Key Vault Ref | Key Vault Ref |
| `ApplicationInsights__InstrumentationKey` | AI monitoring | (unused) | Key Vault Ref | Key Vault Ref |
| `PowerSchool__BaseUrl` | PowerSchool API endpoint | (bypass) | test-ps.school.edu | ps.school.edu |
| `PowerSchool__ApiKey` | PowerSchool authentication | (bypass) | Key Vault Ref | Key Vault Ref |

> **Note**: Double underscore (`__`) maps to nested JSON keys. Key Vault Ref = `@Microsoft.KeyVault(...)`

---
## 6. Local Development (No Changes)

1. Run dev script (installs .NET 9 SDK if needed):
   ```powershell
   cd scripts
   ./dev-run.ps1 -Port 5090 -SqlitePath dev-files.db
   ```
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

# 3. Run migrations
.\deploy.ps1 -Environment Staging -RunMigrations

# OR: Do all at once
.\deploy.ps1 -Environment Staging -CreateResources -DeployApp -RunMigrations
```

---
## 8. Production Deployment Workflow

### **Option A: Promote Tested Staging Build (Recommended)**
```powershell
# 1. Deploy to staging first
.\deploy.ps1 -Environment Staging -CreateResources -DeployApp -RunMigrations

# 2. Test staging thoroughly...

# 3. Create production resources
.\deploy.ps1 -Environment Production -CreateResources

# 4. Promote staging build to production
.\deploy.ps1 -Environment Production -DeployApp -PromoteFromStaging -RunMigrations
```

**Benefits:**
- ✅ Same code tested in staging goes to production
- ✅ Fast deployment (no rebuild)
- ✅ Reduces risk of production-specific build issues
- ✅ Follows industry best practices

### **Option B: Fresh Build for Production**
```powershell
.\deploy.ps1 -Environment Production -CreateResources -DeployApp -RunMigrations
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
- Database migrations (using `-RunMigrations`)

See `scripts/deploy-staging.ps1` and `scripts/deploy-production.ps1` for implementation details.

---


## 10. Database Migrations for Production

### Add SQL Server Support to the Project
1. **Add SQL Server Package**:
```powershell
cd src/FileService.Infrastructure
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
```

2. **Update Program.cs** to support SQL Server:
```csharp
// Add after the existing EF configuration
if (builder.Configuration.GetValue<bool>("Persistence:UseSqlServer"))
{
    var sqlConnection = builder.Configuration.GetConnectionString("Sql");
    builder.Services.AddDbContext<FileServiceDbContext>(options =>
        options.UseSqlServer(sqlConnection));
}
```

3. **Create and Apply Migrations**:

Use the `-RunMigrations` switch in the deploy scripts to apply migrations. See `scripts/deploy-staging.ps1` and `scripts/deploy-production.ps1` for details.

## 11. Infrastructure as Code (Optional Enhancement)

Create `azure-resources.bicep` for reproducible deployments:

```bicep
param location string = resourceGroup().location
param appName string
param sqlAdminPassword string

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-04-01' = {
  name: '${appName}stg${uniqueString(resourceGroup().id)}'
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
}

resource sqlServer 'Microsoft.Sql/servers@2021-02-01-preview' = {
  name: '${appName}-sql-${uniqueString(resourceGroup().id)}'
  location: location
  properties: {
    administratorLogin: 'fsadmin'
    administratorLoginPassword: sqlAdminPassword
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
      netFrameworkVersion: 'v8.0'
      metadata: [{ name: 'CURRENT_STACK', value: 'dotnet' }]
    }
  }
  identity: { type: 'SystemAssigned' }
}
```

Deploy with:
```powershell
az deployment group create -g filesvc-rg --template-file azure-resources.bicep --parameters appName=filesvc-api sqlAdminPassword=YourSecurePassword123!
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
- [ ] Create all Azure resources using the script above
- [ ] Build and deploy application: `dotnet publish` + `az webapp deploy`
- [ ] Run database migrations: `./efbundle.exe`
- [ ] Verify managed identity permissions
- [ ] Test Key Vault secret access

### Post-Deployment
- [ ] Verify `/swagger` endpoint is accessible
- [ ] Test file upload via Swagger UI
- [ ] Verify files are stored in Azure Blob Storage
- [ ] Test file download and delete operations
- [ ] Monitor Application Insights for errors
- [ ] Set up alerts for critical metrics
- [ ] Configure backup policies for SQL Database

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

### Database Monitoring
```sql
-- Monitor database size
SELECT 
    DB_NAME() as DatabaseName,
    SUM(size) * 8 / 1024 as DatabaseSizeInMB
FROM sys.database_files;

-- Monitor file operations
SELECT 
    COUNT(*) as TotalFiles,
    SUM(FileSizeBytes) / 1024 / 1024 as TotalSizeInMB,
    CreatedBy,
    COUNT(CASE WHEN CreatedAt > DATEADD(day, -1, GETDATE()) THEN 1 END) as FilesLast24Hours
FROM FileRecords
GROUP BY CreatedBy;
```

## 14. Backup & Disaster Recovery

### Database Backups
```powershell
# Configure automated backups (7-day retention)
az sql db update -s <sql-server-name> -n <db-name> -g filesvc-rg --backup-storage-redundancy Local

# Manual backup
az sql db export -s <sql-server-name> -n <db-name> -g filesvc-rg --admin-user <admin-user> --admin-password <password> --storage-key-type StorageAccessKey --storage-key <storage-key> --storage-uri "https://<storage-account>.blob.core.windows.net/backups/backup-$(Get-Date -Format 'yyyyMMdd-HHmmss').bacpac"
```

### Blob Storage Backups
```powershell
# Enable soft delete for blobs (30-day retention)
az storage account blob-service-properties update --account-name <storage-account> --enable-delete-retention true --delete-retention-days 30

# Enable versioning
az storage account blob-service-properties update --account-name <storage-account> --enable-versioning true
```

---
## 15. Summary

The deployment guide now provides:

✅ **Complete Azure Resource Provisioning** - All necessary services  
✅ **Security Best Practices** - Managed Identity, Key Vault, HTTPS  
✅ **Production Database** - Azure SQL with migrations  
✅ **Monitoring & Alerting** - Application Insights with custom metrics  
✅ **Infrastructure as Code** - Bicep templates for reproducible deployments  
✅ **Comprehensive Checklist** - Pre/post deployment validation  
✅ **Backup & Recovery** - Database and blob storage protection  

This guide ensures a production-ready deployment with enterprise-grade security, monitoring, and reliability! 🚀

## 16. Troubleshooting Multi-Environment Issues

| Environment | Symptom | Possible Cause | Fix |
|-------------|---------|----------------|-----|
| **Staging** | 401 responses | Missing auth headers | Use real PowerSchool headers or check staging credentials |
| **Staging** | Upload works but download 404 | Wrong container name | Verify `userfiles-staging` container exists |
| **Staging** | Database connection fails | Key Vault access issue | Check managed identity permissions |
| **Production** | Swagger disabled | Production config | Expected behavior - use staging for API testing |
| **Production** | High memory usage | Large file processing | Monitor App Insights and consider scaling up |
| **Both** | Key Vault secret not found | Wrong secret name format | Use exact format: `BlobStorage--ConnectionString` |
| **Both** | SQL timeout | Database overloaded | Scale up database tier or optimize queries |

### Common Resolution Steps:

#### 1. Key Vault Access Issues
```powershell
# Check managed identity permissions
az webapp identity show -n <webapp-name> -g <resource-group>
az keyvault show -n <keyvault-name> -g <resource-group> --query properties.accessPolicies
```

#### 2. Database Connection Problems
```powershell
# Test SQL connection manually
az sql db show -s <sql-server> -n <db-name> -g <resource-group>
az sql server firewall-rule list -s <sql-server> -g <resource-group>
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
| **SQL Database** | $5 (Basic) | $30 (S2) | Production may need higher tier |
| **Storage Account** | $2 | $5 | Includes backup redundancy |
| **Application Insights** | $0-5 | $10-20 | Based on telemetry volume |
| **Key Vault** | $1 | $1 | Secret operations |
| **Total/Month** | **~$21** | **~$119** | Approximate costs |

### Cost Optimization Tips:
- 🔄 **Auto-shutdown staging** during non-business hours
- 🔄 **Scale down staging** App Service to B1 or even F1 (free tier)
- 🔄 **Use Basic SQL tier** for staging (adequate for testing)
- 🔄 **Monitor usage** with Azure Cost Management

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
