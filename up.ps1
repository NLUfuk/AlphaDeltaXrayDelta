<#
  up.ps1 - bring the whole CRM+Kanban stack up with one command.
  Generates .env with secure random secrets on first run, then builds and starts
  API + MSSQL + MinIO + nginx via docker compose. Idempotent: re-run to rebuild.

  Usage:  ./up.ps1            # build + start (detached)
          ./up.ps1 -Down      # stop and remove containers
          ./up.ps1 -Reset     # stop and wipe volumes (DB + files) too
#>
[CmdletBinding()]
param([switch]$Down, [switch]$Reset)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

function Rand([int]$bytes) {
  $b = New-Object 'byte[]' $bytes
  $rng = [System.Security.Cryptography.RNGCryptoServiceProvider]::new()
  try { $rng.GetBytes($b) } finally { $rng.Dispose() }
  [Convert]::ToBase64String($b)
}
# Password guaranteed to satisfy MSSQL complexity (upper+lower+digit+symbol, 8+).
function RandPassword { 'Aa1!' + (Rand 24).Replace('/', '_').Replace('+', '-') }

if ($Down -or $Reset) {
  if ($Reset) { docker compose down -v; Write-Host "Stack down, volumes wiped." }
  else        { docker compose down;    Write-Host "Stack stopped." }
  return
}

# --- .env: create once with real secrets, never overwrite an existing one ---
if (-not (Test-Path .env)) {
  $saPw    = RandPassword
  $minioPw = RandPassword
  $adminPw = RandPassword
  @"
MSSQL_SA_PASSWORD=$saPw
JWT_SIGNING_KEY=$(Rand 48)
JWT_ISSUER=CrmKanban
JWT_AUDIENCE=CrmKanban
S3_ACCESS_KEY=minioadmin
S3_SECRET_KEY=$minioPw
S3_BUCKET=crm-kanban
SUPERADMIN_EMAIL=admin@example.com
SUPERADMIN_PASSWORD=$adminPw
EMAIL_PROVIDER=log
WEB_PORT=8080
APP_PUBLIC_BASE_URL=http://localhost:8080
SEED_DEMO=true
"@ | Set-Content -Path .env -Encoding utf8
  Write-Host "Generated .env with random secrets." -ForegroundColor Green
  Write-Host "  Super admin: admin@example.com / $adminPw" -ForegroundColor Yellow
  Write-Host "  (change the email in .env before running again if you want a different one)"
} else {
  Write-Host ".env already exists - using it."
}

# Read WEB_PORT for the final message.
$webPort = (Get-Content .env | Where-Object { $_ -match '^WEB_PORT=' }) -replace '^WEB_PORT=', ''
if (-not $webPort) { $webPort = '8080' }

# ponytail: BuildKit corrupts its session sharedkey header when the context path
# has non-ASCII chars (this repo lives under "...\Yeni klasör"). Use the classic
# builder. Drop these two lines if the repo ever moves to an ASCII-only path.
$env:DOCKER_BUILDKIT = '0'
$env:COMPOSE_DOCKER_CLI_BUILD = '0'

Write-Host "Building and starting the stack..." -ForegroundColor Cyan
# docker writes build progress to stderr; under ErrorActionPreference='Stop' PowerShell turns that
# into a terminating NativeCommandError and the script "fails" on a successful build. The exit code
# below is the real verdict, so silence the stream-level error for this one call.
$ErrorActionPreference = 'Continue'
docker compose up --build -d
$code = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
if ($code -ne 0) { throw "docker compose failed (exit $code)." }

# Wait for the SPA/API to answer (API applies migrations + seed on startup).
Write-Host "Waiting for the app to become ready..." -NoNewline
$url = "http://localhost:$webPort/"
$ready = $false
foreach ($i in 1..60) {
  try {
    Invoke-WebRequest -Uri $url -TimeoutSec 3 -UseBasicParsing | Out-Null
    $ready = $true; break
  } catch { Start-Sleep -Seconds 3; Write-Host '.' -NoNewline }
}
Write-Host ''
if ($ready) {
  Write-Host "Up. App: $url" -ForegroundColor Green
  Write-Host "MinIO console: http://localhost:9001  (creds in .env: S3_ACCESS_KEY / S3_SECRET_KEY)"
  Write-Host "Log in with SUPERADMIN_EMAIL / SUPERADMIN_PASSWORD from .env (forced password change on first login)."
} else {
  Write-Host "Containers started but $url did not respond in time." -ForegroundColor Yellow
  Write-Host "Check logs:  docker compose logs -f api"
}
