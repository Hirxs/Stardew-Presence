$ErrorActionPreference = "Stop"

$projectDir = (Resolve-Path "$PSScriptRoot\..").Path
$stagingDir = Join-Path $projectDir "bin\Release\package\StardewPresence"

if (Test-Path $stagingDir) {
    Remove-Item $stagingDir -Recurse -Force
}
New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

# Copy release files
Copy-Item (Join-Path $projectDir "bin\Release\net6.0\StardewPresence.dll") -Destination $stagingDir -Force
Copy-Item (Join-Path $projectDir "bin\Release\net6.0\StardewPresence.pdb") -Destination $stagingDir -Force
Copy-Item (Join-Path $projectDir "manifest.json") -Destination $stagingDir -Force
Copy-Item (Join-Path $projectDir "ui_layout.json") -Destination $stagingDir -Force
if (Test-Path (Join-Path $projectDir "assets")) {
    Copy-Item (Join-Path $projectDir "assets") -Destination $stagingDir -Recurse -Force
}
Copy-Item (Join-Path $projectDir "i18n") -Destination $stagingDir -Recurse -Force

# Unblock files
Get-ChildItem -Path $stagingDir -Recurse | Unblock-File

# Output zip paths
$zipDestinations = @(
    (Join-Path $projectDir "..\StardewPresence-v0.2.0-BETA.zip"),
    "C:\Users\ale_y\Downloads\StardewPresence-v0.2.0-BETA.zip"
)

foreach ($zipFile in $zipDestinations) {
    if (Test-Path $zipFile) {
        Remove-Item $zipFile -Force
    }
    Compress-Archive -Path $stagingDir -DestinationPath $zipFile -Force
    Write-Host "[ZIP EXPORTED] -> $zipFile" -ForegroundColor Green
}
