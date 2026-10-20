# Builds the plugin and packs dist\HDT-BG-AutoReconnect-v<version>.zip for a GitHub release.
# Requires the .NET SDK and an installed Hearthstone Deck Tracker (see README).
param([string]$HdtDir = "$env:LOCALAPPDATA\HearthstoneDeckTracker\current")
$ErrorActionPreference = 'Stop'

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
$dotnet = if ($dotnet) { $dotnet.Source } else { "$env:USERPROFILE\.dotnet\dotnet.exe" }
$proj = Join-Path $PSScriptRoot 'src\HDT_BG_AutoReconnect\HDT_BG_AutoReconnect.csproj'

& $dotnet build $proj -c Release -nologo "-p:HdtDir=$HdtDir"
if ($LASTEXITCODE) { throw "build failed" }

$version = ([xml](Get-Content $proj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
$dll = Join-Path $PSScriptRoot 'src\HDT_BG_AutoReconnect\bin\Release\net472\HDT_BG_AutoReconnect.dll'
$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null
$zip = Join-Path $dist "HDT-BG-AutoReconnect-v$version.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path $dll, (Join-Path $PSScriptRoot 'LICENSE') -DestinationPath $zip
"packed $zip"
