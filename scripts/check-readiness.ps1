<#
  Readiness check for the interview-dotnet-api live coding round.
  Verifies your machine can build and run this .NET 10 project before the session.

  Usage:
    pwsh ./check-readiness.ps1            # run all checks
    pwsh ./check-readiness.ps1 -SkipRun   # skip the launch/HTTP check

  If PowerShell blocks the script, run:
    pwsh -ExecutionPolicy Bypass -File ./check-readiness.ps1
#>
[CmdletBinding()]
param([switch]$SkipRun)

$ErrorActionPreference = 'Continue'
$script:RequiredFailed = $false

function Write-Pass($m) { Write-Host "  PASS  " -ForegroundColor Green -NoNewline; Write-Host $m }
function Write-Fail($m) { Write-Host "  FAIL  " -ForegroundColor Red   -NoNewline; Write-Host $m; $script:RequiredFailed = $true }
function Write-Note($m) { Write-Host "  NOTE  " -ForegroundColor Yellow -NoNewline; Write-Host $m }
function Write-Head($m) { Write-Host ""; Write-Host $m -ForegroundColor Cyan }

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Split-Path -Parent $ScriptDir
Set-Location $ProjectDir
$OpenApiPort = 5080
$Dll = "bin/Debug/net10.0/interview-dotnet-api.dll"

Write-Host "interview-dotnet-api - device readiness check" -ForegroundColor White
Write-Host "Project: $ProjectDir"

# --- 1. .NET 10 SDK --------------------------------------------------------
Write-Head "1. .NET 10 SDK"
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
  Write-Fail "'dotnet' not found on PATH. Install the .NET 10 SDK: https://dotnet.microsoft.com/download/dotnet/10.0"
} else {
  $sdks = & dotnet --list-sdks
  $ten = $sdks | Where-Object { $_ -match '^10\.' }
  if ($ten) {
    $ver = ($ten | Select-Object -First 1).Split(' ')[0]
    Write-Pass "found $ver (dotnet --version: $(& dotnet --version))"
  } else {
    Write-Fail "no 10.x SDK installed. Get .NET 10: https://dotnet.microsoft.com/download/dotnet/10.0"
  }
}

# --- 2. EF Core CLI tools --------------------------------------------------
Write-Head "2. EF Core CLI tools"
$efVer = & dotnet ef --version 2>$null
if ($LASTEXITCODE -eq 0 -and $efVer) {
  Write-Pass "dotnet ef $($efVer | Select-Object -Last 1)"
} else {
  Write-Fail "'dotnet ef' not available. Install: dotnet tool install --global dotnet-ef"
}

# --- 3. IDE (informational) ------------------------------------------------
Write-Head "3. IDE"
$ideFound = $false

# CLI launchers on PATH
foreach ($ide in @(@{c='code';n="VS Code ('code' on PATH) - install the C# Dev Kit extension"}, @{c='devenv';n="Visual Studio ('devenv' on PATH)"}, @{c='rider';n="Rider ('rider' on PATH)"})) {
  if (Get-Command $ide.c -ErrorAction SilentlyContinue) { Write-Pass "$($ide.n) detected"; $ideFound = $true }
}

# Standard install locations (the CLI launcher is often not on PATH)
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (Test-Path $vswhere) {
  $vs = & $vswhere -latest -property displayName 2>$null
  if ($vs) { Write-Pass "Visual Studio found via vswhere: $($vs | Select-Object -First 1)"; $ideFound = $true }
}
$vscodeExe = Join-Path $env:LOCALAPPDATA 'Programs\Microsoft VS Code\Code.exe'
if (Test-Path $vscodeExe) { Write-Pass "VS Code found in %LOCALAPPDATA% - install the C# Dev Kit extension"; $ideFound = $true }
if (Get-ChildItem 'C:\Program Files\JetBrains' -Filter '*Rider*' -Directory -ErrorAction SilentlyContinue) {
  Write-Pass "Rider found in C:\Program Files\JetBrains"; $ideFound = $true
}

if (-not $ideFound) { Write-Note "No IDE auto-detected (checks PATH + standard install paths). If you already have Visual Studio, VS Code (+ C# Dev Kit), or Rider installed, you're fine - this check can miss non-standard locations." }

# --- 4. HTTP client (informational) ----------------------------------------
Write-Head "4. HTTP client"
if (Get-Command curl -ErrorAction SilentlyContinue) {
  Write-Pass "curl detected"
} else {
  Write-Note "curl not found - Swagger UI, Postman, or a .http file in your IDE work too."
}

# --- 5. AI assistant (reminder) --------------------------------------------
# NOTE: sign-in state can't be detected from a script (it lives in browser
# cookies or an extension's private store). We can only detect *installed* tooling.
Write-Head "5. AI assistant"
$ai = @()
if (Get-Command claude -ErrorAction SilentlyContinue) { $ai += 'Claude Code CLI' }
if (Get-Command code -ErrorAction SilentlyContinue) {
  $exts = & code --list-extensions 2>$null | Where-Object { $_ -match 'copilot|claude|continue|codeium|sourcegraph.cody|tabnine' }
  if ($exts) { $ai += "VS Code extensions: $($exts -join ',')" }
}
if (Get-Command gh -ErrorAction SilentlyContinue) {
  if (& gh extension list 2>$null | Select-String -Pattern 'copilot' -Quiet) { $ai += 'gh copilot' }
}
$cursorWin = Join-Path $env:LOCALAPPDATA 'Programs\cursor\Cursor.exe'
if (Test-Path $cursorWin) { $ai += 'Cursor app' }

if ($ai.Count -gt 0) {
  Write-Note "Detected AI tooling: $($ai -join '; ') - installation only; confirm you're actually signed in (sign-in state can't be verified from a script)."
} else {
  Write-Note "No AI tooling auto-detected, and sign-in state can't be verified from a script. Make sure your assistant (browser tab or IDE extension) is installed and signed in."
}

# --- 6. Build --------------------------------------------------------------
Write-Head "6. Build"
$buildOk = $false
$buildLog = & dotnet build -v q 2>&1
if ($LASTEXITCODE -eq 0) {
  Write-Pass "dotnet build compiles cleanly"
  $buildOk = $true
} else {
  Write-Fail "dotnet build failed. Output:"
  $buildLog | Select-Object -Last 20 | ForEach-Object { Write-Host "        $_" }
}

# --- 7. Run + OpenAPI ------------------------------------------------------
Write-Head "7. Run & serve OpenAPI"
if ($SkipRun) {
  Write-Note "skipped (-SkipRun)"
} elseif (-not $buildOk) {
  Write-Note "skipped (build did not succeed)"
} elseif (-not (Test-Path $Dll)) {
  Write-Note "skipped (built assembly not found at $Dll)"
} else {
  $url = "http://localhost:$OpenApiPort/openapi/v1.json"
  $env:ASPNETCORE_ENVIRONMENT = 'Development'
  $env:ASPNETCORE_URLS = "http://localhost:$OpenApiPort"
  $logPath = Join-Path ([System.IO.Path]::GetTempPath()) 'ifa_run.log'
  $proc = Start-Process -FilePath 'dotnet' -ArgumentList $Dll -PassThru -NoNewWindow `
            -RedirectStandardOutput $logPath -RedirectStandardError "$logPath.err"
  $ok = $false
  for ($i = 0; $i -lt 30; $i++) {
    if ($proc.HasExited) { break }
    try {
      $resp = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
      if ($resp.StatusCode -eq 200) { $ok = $true; break }
    } catch { Start-Sleep -Seconds 1 }
  }
  if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
  if ($ok) {
    Write-Pass "app started and served OpenAPI at $url"
  } else {
    Write-Fail "could not reach $url within 30s. Run log:"
    if (Test-Path $logPath)       { Get-Content $logPath       -Tail 20 | ForEach-Object { Write-Host "        $_" } }
    if (Test-Path "$logPath.err") { Get-Content "$logPath.err" -Tail 20 | ForEach-Object { Write-Host "        $_" } }
  }
}

# --- summary ---------------------------------------------------------------
Write-Host ""
if (-not $script:RequiredFailed) {
  Write-Host "All required checks passed - you're ready for the round." -ForegroundColor Green
  exit 0
} else {
  Write-Host "Some required checks failed - please resolve the items marked FAIL above." -ForegroundColor Red
  exit 1
}
