#!/usr/bin/env pwsh
# Simplified Azure China deployment script

param(
    [string]$SubscriptionId,
    [string]$Location = "chinaeast", 
    [string]$ResourcePrefix = "filesvc-stg"
)

$ResourceGroup = "${ResourcePrefix}-rg"
$storageAccount = "filesvcstg" + $Location
$keyVaultName = "${ResourcePrefix}-kv"
$webAppName = "${ResourcePrefix}-app"

Write-Host "=== Azure File Service Deployment ===" -ForegroundColor Cyan

# Set Azure China Cloud
az cloud set --name AzureChinaCloud

# Auto-detect subscription if not provided
if (-not $SubscriptionId) {
    $SubscriptionId = az account show --query id --output tsv
}

az account set --subscription $SubscriptionId
Write-Host "Using subscription: $SubscriptionId" -ForegroundColor Green

Write-Host "Ready to deploy to Azure China with Table Storage" -ForegroundColor Green
Write-Host "Resource Group: $ResourceGroup"
Write-Host "Storage Account: $storageAccount" 
Write-Host "Web App: $webAppName"
Write-Host ""
Write-Host "Run the full deployment? This script is a syntax test." -ForegroundColor Yellow