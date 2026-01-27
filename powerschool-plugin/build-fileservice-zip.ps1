# Build FileService Tools Plugin ZIP
# Removes existing zip files and creates a clean fileservice.zip with plugin contents

$basePath = "c:\Users\wang_\Documents\Codes\AzureFileSys\AzureFileSys\powerschool-plugin\FileServiceTools"
$outputZip = Join-Path $basePath "fileservice.zip"

# Verify base path exists
if (-not (Test-Path $basePath)) {
    Write-Error "FileServiceTools directory not found: $basePath"
    exit 1
}

# Delete any existing zip files
Write-Host "Cleaning up existing zip files..."
Get-ChildItem -Path $basePath -Filter "*.zip" -File | ForEach-Object {
    Write-Host "  Deleting: $($_.Name)"
    Remove-Item $_.FullName -Force
}

# Verify required items exist
$itemsToZip = @(
    "user_schema_root",
    "web_root",
    "plugin.xml"
)

foreach ($item in $itemsToZip) {
    $itemPath = Join-Path $basePath $item
    if (-not (Test-Path $itemPath)) {
        Write-Error "Required item not found: $item"
        exit 1
    }
}

# Create the zip file using .NET to control path separators
Write-Host "Creating fileservice.zip..."

# Remove if exists
if (Test-Path $outputZip) {
    Remove-Item $outputZip -Force
}

# Load compression assemblies
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

# Create zip archive
$zipArchive = [System.IO.Compression.ZipFile]::Open($outputZip, [System.IO.Compression.ZipArchiveMode]::Create)

try {
    # Add plugin.xml
    Write-Host "  Adding plugin.xml"
    $pluginXmlPath = Join-Path $basePath "plugin.xml"
    $entry = $zipArchive.CreateEntry("plugin.xml")
    $entryStream = $entry.Open()
    $fileStream = [System.IO.File]::OpenRead($pluginXmlPath)
    $fileStream.CopyTo($entryStream)
    $fileStream.Close()
    $entryStream.Close()
    
    # Add directories with forward slashes
    @("user_schema_root", "web_root") | ForEach-Object {
        $dirName = $_
        $dirPath = Join-Path $basePath $dirName
        Write-Host "  Adding $dirName"
        
        Get-ChildItem -Path $dirPath -Recurse -File | ForEach-Object {
            $file = $_
            $relativePath = $file.FullName.Substring($basePath.Length + 1)
            # Convert backslashes to forward slashes for zip compatibility
            $zipPath = $relativePath.Replace('\', '/')
            
            $entry = $zipArchive.CreateEntry($zipPath)
            $entryStream = $entry.Open()
            $fileStream = [System.IO.File]::OpenRead($file.FullName)
            $fileStream.CopyTo($entryStream)
            $fileStream.Close()
            $entryStream.Close()
        }
    }
    
} finally {
    $zipArchive.Dispose()
}

# Verify
$zipInfo = Get-Item $outputZip
Write-Host "`nSuccess! Created fileservice.zip"
Write-Host "  Location: $outputZip"
Write-Host "  Size: $([Math]::Round($zipInfo.Length / 1KB, 2)) KB"
