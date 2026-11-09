param(
    [int]$Port = 52000
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   Starting CampusSafe Twin Environment   " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# checks if the app uses a remote db (supabase) - then local postgres isnt needed,,,
$webConfig = Join-Path $PSScriptRoot "Web.config"
$dbIsRemote = $false
if (Test-Path $webConfig) {
    $connStr = ([xml](Get-Content $webConfig)).configuration.connectionStrings.add |
        Where-Object { $_.name -eq "CampusSafe" } | Select-Object -ExpandProperty connectionString
    if ($connStr -and $connStr -notmatch "Host=(localhost|127\.0\.0\.1)") { $dbIsRemote = $true }
}

# finds the postgres install (portable locations checked in order),,,
$pgCandidates = @(
    (Join-Path $env:LOCALAPPDATA "CampusSafeTwin\PostgreSQL"),
    "C:\PostgreSQL",
    "C:\Program Files\PostgreSQL\16"
)
$pgHome = $null
foreach ($cand in $pgCandidates) {
    if (Test-Path (Join-Path $cand "bin\postgres.exe")) { $pgHome = $cand; break }
}

$pgDataCandidates = @(
    (Join-Path $env:LOCALAPPDATA "CampusSafeTwin\data"),
    "C:\PostgreSQL\data"
)
$pgData = $null
foreach ($cand in $pgDataCandidates) {
    if (Test-Path (Join-Path $cand "PG_VERSION")) { $pgData = $cand; break }
}

# starts postgres if its not running yet (skipped when the db is remote/supabase),,,
$pgProc = Get-Process -Name postgres -ErrorAction SilentlyContinue
if ($dbIsRemote) {
    Write-Host "[1/3] Remote database configured (Supabase) - no local PostgreSQL needed." -ForegroundColor Green
} elseif (-not $pgProc) {
    if ($pgHome -and $pgData) {
        Write-Host "[1/3] Starting PostgreSQL ($pgHome)..." -ForegroundColor Yellow
        $pidFile = Join-Path $pgData "postmaster.pid"
        if (Test-Path $pidFile) { Remove-Item $pidFile -Force -ErrorAction SilentlyContinue }
        $log = Join-Path (Split-Path $pgData -Parent) "postgres.log"
        Start-Process (Join-Path $pgHome "bin\pg_ctl.exe") -ArgumentList "-D", "`"$pgData`"", "-l", "`"$log`"", "start" -WindowStyle Hidden
        # waits until the db accepts connections...
        $ready = $false
        for ($i = 0; $i -lt 30; $i++) {
            Start-Sleep -Seconds 1
            & (Join-Path $pgHome "bin\pg_isready.exe") -h localhost -p 5432 2>$null | Out-Null
            if ($LASTEXITCODE -eq 0) { $ready = $true; break }
        }
        if ($ready) {
            Write-Host "      PostgreSQL is accepting connections." -ForegroundColor Green
        } else {
            Write-Host "      WARNING: PostgreSQL did not report ready. Check $log" -ForegroundColor Red
        }
    } else {
        Write-Host "[1/3] WARNING: PostgreSQL install not found. The app needs it!" -ForegroundColor Red
    }
} else {
    Write-Host "[1/3] PostgreSQL is already running." -ForegroundColor Green
}

# starts iis express web server...
$iisProc = Get-Process -Name iisexpress -ErrorAction SilentlyContinue
if (-not $iisProc) {
    $iisExe = "C:\Program Files\IIS Express\iisexpress.exe"
    if (-not (Test-Path $iisExe)) {
        $iisExe = "C:\Program Files (x86)\IIS Express\iisexpress.exe"
    }

    $appPath = $PSScriptRoot
    Write-Host "[2/3] Starting IIS Express on port $Port..." -ForegroundColor Yellow
    Start-Process $iisExe -ArgumentList "/path:`"$appPath`"", "/port:$Port" -WindowStyle Minimized
    Start-Sleep -Seconds 3
    Write-Host "      IIS Express started successfully on port $Port." -ForegroundColor Green
} else {
    Write-Host "[2/3] IIS Express is already running." -ForegroundColor Green
}

# opens web browser to login page,,,
$url = "http://localhost:$Port/Login.aspx"
Write-Host "[3/3] Opening browser at $url..." -ForegroundColor Yellow
Start-Process $url

Write-Host "`nAll services running!" -ForegroundColor Cyan
Write-Host "URL: $url" -ForegroundColor White
Write-Host "Demo Credentials:" -ForegroundColor White
Write-Host "  - Student:    sipho@student.nwu.ac.za / CampusSafe2026!"
Write-Host "  - Guard:      kobus@guard.nwu.ac.za / CampusSafe2026!"
Write-Host "  - Supervisor: pieta@security.nwu.ac.za / CampusSafe2026!"
Write-Host "  - Admin:      supervisor@campussafe.ac.za / SupervisorPass123!`n"
