[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$installerScript = Join-Path $repositoryRoot 'installer\smoke\YahooMonthPrint.ToolchainSmoke.iss'
$installerOutput = Join-Path $repositoryRoot 'artifacts\installer-smoke'
. (Join-Path $PSScriptRoot 'InnoSetup.ps1')
$innoCompilerPath = Resolve-InnoCompiler

if ([string]::IsNullOrWhiteSpace($innoCompilerPath)) {
    throw 'Inno Setup 6 compiler (ISCC.exe) was not found. Run eng/verify-tools.ps1 for setup guidance.'
}

New-Item -ItemType Directory -Force -Path $installerOutput | Out-Null
$smokeInstaller = Join-Path $installerOutput 'YahooMonthPrint-Toolchain-Smoke.exe'
Remove-Item -LiteralPath $smokeInstaller -Force -ErrorAction SilentlyContinue

& $innoCompilerPath '/Qp' "/O$installerOutput" $installerScript
if ($LASTEXITCODE -ne 0) {
    throw "ISCC.exe failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $smokeInstaller)) {
    throw "Expected installer was not produced: $smokeInstaller"
}

Write-Host "Inno Setup smoke artifact: $smokeInstaller"
