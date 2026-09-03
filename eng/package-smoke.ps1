[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$installerScript = Join-Path $repositoryRoot 'installer\smoke\YahooMonthPrint.ToolchainSmoke.iss'
$installerOutput = Join-Path $repositoryRoot 'artifacts\installer-smoke'

$innoCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if ($null -ne $innoCommand) {
    $innoCompilerPath = $innoCommand.Source
}
else {
    $innoCandidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        'C:\Program Files\Inno Setup 6\ISCC.exe'
    )
    $innoCompilerPath = $innoCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($innoCompilerPath)) {
    throw 'Inno Setup 6 compiler (ISCC.exe) was not found. Run eng/verify-tools.ps1 for setup guidance.'
}

New-Item -ItemType Directory -Force -Path $installerOutput | Out-Null
& $innoCompilerPath '/Qp' "/O$installerOutput" $installerScript
if ($LASTEXITCODE -ne 0) {
    throw "ISCC.exe failed with exit code $LASTEXITCODE."
}

$smokeInstaller = Join-Path $installerOutput 'YahooMonthPrint-Toolchain-Smoke.exe'
if (-not (Test-Path -LiteralPath $smokeInstaller)) {
    throw "Expected installer was not produced: $smokeInstaller"
}

Write-Host "Inno Setup smoke artifact: $smokeInstaller"
