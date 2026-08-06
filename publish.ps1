<#
  publish.ps1 — build a single-folder, self-contained deploy for MonsterASP.NET (IIS, one site).

  Produces ./publish containing the API exe + the SPA in wwwroot + the .NET 10 runtime (self-contained,
  so the host needs no .NET installed) + web.config (in-process ASP.NET Core Module). Upload the whole
  folder to your MonsterASP.NET site root, then set config (see docs/DEPLOY-monsterasp.md).

  Usage:  ./publish.ps1            # win-x64, self-contained, output ./publish
#>
[CmdletBinding()]
param([string]$Runtime = 'win-x64', [string]$Out = 'publish')

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$api = 'src/CrmKanban.Api'
$wwwroot = "$api/wwwroot"

Write-Host '1/3  Building the SPA...' -ForegroundColor Cyan
Push-Location frontend
try {
  if (-not (Test-Path node_modules)) { npm ci }
  npm run build
  if ($LASTEXITCODE -ne 0) { throw 'frontend build failed' }
} finally { Pop-Location }

Write-Host '2/3  Copying the SPA into the API wwwroot...' -ForegroundColor Cyan
if (Test-Path $wwwroot) { Remove-Item $wwwroot -Recurse -Force }
New-Item -ItemType Directory -Force $wwwroot | Out-Null
Copy-Item 'frontend/dist/*' $wwwroot -Recurse -Force

Write-Host "3/3  Publishing self-contained ($Runtime) to ./$Out ..." -ForegroundColor Cyan
if (Test-Path $Out) { Remove-Item $Out -Recurse -Force }
dotnet publish $api -c Release -r $Runtime --self-contained true -o $Out
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

Write-Host "Done. Upload the contents of ./$Out to your MonsterASP.NET site root." -ForegroundColor Green
Write-Host 'Then set ConnectionStrings__Default, Jwt__SigningKey, SuperAdmin__*, Email__* (see docs/DEPLOY-monsterasp.md).'
