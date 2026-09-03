$ErrorActionPreference = "Continue"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Compilando y Exportando Stardew Presence" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$projectDir = (Resolve-Path "$PSScriptRoot\..").Path
$csproj = Join-Path $projectDir "StardewDiscordRPC.csproj"
$dotnet = "C:\Users\ale_y\dotnet_sdk\dotnet.exe"

& $dotnet build $csproj -c Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] La compilacion fallo." -ForegroundColor Red
    exit 1
}

$srcDll = Join-Path $projectDir "bin\Release\net6.0\StardewDiscordRPC.dll"
$srcPdb = Join-Path $projectDir "bin\Release\net6.0\StardewDiscordRPC.pdb"

$destDirs = @(
    "C:\Users\ale_y\curseforge\stardew-valley\modpacks\Test\mods\StardewDiscordRPC",
    "C:\Users\ale_y\curseforge\stardew-valley\modpacks\Everything Dew\mods\StardewDiscordRPC",
    "C:\XboxGames\Stardew Valley\Content\Mods\StardewDiscordRPC",
    "C:\Users\ale_y\curseforge\stardew-valley\modpacks\Test\mods\StardewPresence",
    "C:\Users\ale_y\curseforge\stardew-valley\modpacks\Everything Dew\mods\StardewPresence",
    "C:\XboxGames\Stardew Valley\Content\Mods\StardewPresence"
)

# Unblock source binaries
Get-ChildItem -Path (Join-Path $projectDir "bin") -Recurse | Unblock-File -ErrorAction SilentlyContinue

foreach ($dir in $destDirs) {
    if (Test-Path $dir) {
        # Clean locks/old
        Get-ChildItem -Path $dir -Filter "*.old*" | Remove-Item -Force -ErrorAction SilentlyContinue

        $destDll = Join-Path $dir "StardewDiscordRPC.dll"
        $destPdb = Join-Path $dir "StardewDiscordRPC.pdb"

        if (Test-Path $destDll) {
            try {
                Remove-Item $destDll -Force -ErrorAction Stop
            } catch {
                $lockName = "StardewDiscordRPC.dll.oldLock_" + [Guid]::NewGuid().ToString().Substring(0, 6)
                Rename-Item $destDll $lockName -Force -ErrorAction SilentlyContinue
            }
        }
        if (Test-Path $destPdb) {
            try {
                Remove-Item $destPdb -Force -ErrorAction Stop
            } catch {
                $lockName = "StardewDiscordRPC.pdb.oldLock_" + [Guid]::NewGuid().ToString().Substring(0, 6)
                Rename-Item $destPdb $lockName -Force -ErrorAction SilentlyContinue
            }
        }

        # Copy DLL and PDB
        Copy-Item $srcDll -Destination $destDll -Force -ErrorAction SilentlyContinue
        Copy-Item $srcPdb -Destination $destPdb -Force -ErrorAction SilentlyContinue

        # Copy ui_layout.json
        Copy-Item (Join-Path $projectDir "ui_layout.json") -Destination (Join-Path $dir "ui_layout.json") -Force -ErrorAction SilentlyContinue

        # Copy i18n
        $i18nDest = Join-Path $dir "i18n"
        if (!(Test-Path $i18nDest)) { New-Item $i18nDest -ItemType Directory -Force | Out-Null }
        Copy-Item (Join-Path $projectDir "i18n\*") -Destination $i18nDest -Recurse -Force -ErrorAction SilentlyContinue

        # Copy manifest
        Copy-Item (Join-Path $projectDir "manifest.json") -Destination (Join-Path $dir "manifest.json") -Force -ErrorAction SilentlyContinue

        # Copy assets
        $assetsDest = Join-Path $dir "assets"
        if (!(Test-Path $assetsDest)) { New-Item $assetsDest -ItemType Directory -Force | Out-Null }
        Copy-Item (Join-Path $projectDir "assets\*") -Destination $assetsDest -Recurse -Force -ErrorAction SilentlyContinue

        # Unblock files
        Get-ChildItem -Path $dir -Recurse | Unblock-File -ErrorAction SilentlyContinue

        Write-Host "[DEPLOYED OK] -> $dir" -ForegroundColor Green
    }
}

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  *** MOD EXPORTADO CON EXITO! ***" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Cyan
