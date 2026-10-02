# Build tuisweeper (Release, single-file, framework-dependent) and install onto PATH.
# Usage: scripts\install.ps1 [-InstallDir <dir>]
# Default dir: %LOCALAPPDATA%\Programs\tuisweeper. Linux/macOS: use install.sh.
#Requires -Version 5
[CmdletBinding()]
param(
  [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\tuisweeper')
)
$ErrorActionPreference = 'Stop'

# Script lives in scripts\, the project lives in src\ next to it.
$project = Join-Path (Split-Path -Parent $PSScriptRoot) 'src'

$arch = if ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture -eq 'Arm64') { 'arm64' } else { 'x64' }
$rid = "win-$arch"

Write-Host "Publishing tuisweeper ($rid)..."
dotnet publish $project -c Release -r $rid --no-self-contained -p:PublishSingleFile=true
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$binary = Join-Path $project "bin\Release\net10.0\$rid\publish\Tuisweeper.exe"
if (-not (Test-Path $binary)) { throw "Publish succeeded but binary not found at $binary" }

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
$target = Join-Path $InstallDir 'tuisweeper.exe'
Copy-Item $binary $target -Force
Write-Host "Installed: $target"

$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
if (($userPath -split ';') -notcontains $InstallDir) {
  Write-Host "Note: $InstallDir is not on your PATH. Add it for this user with:"
  Write-Host "  setx PATH `"$InstallDir;`$($env:Path)`""
}
