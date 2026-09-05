[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+(\.\d+)?$')]
    [string]$Version,

    [string]$PublishDirectory = 'artifacts\publish\win-x64',

    [string]$OutputDirectory = 'artifacts\installer',

    [string]$SignToolPath,

    [ValidatePattern('^[0-9A-Fa-f]{40}$')]
    [string]$SigningCertificateThumbprint,

    [string]$TimestampUrl = 'https://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$installerScript = Join-Path $repositoryRoot 'installer\YahooMonthPrint.iss'
$resolvedPublishDirectory = (Resolve-Path -LiteralPath (Join-Path $repositoryRoot $PublishDirectory)).Path
$resolvedOutputDirectory = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
. (Join-Path $PSScriptRoot 'InnoSetup.ps1')
$innoCompilerPath = Resolve-InnoCompiler

if ([string]::IsNullOrWhiteSpace($innoCompilerPath)) {
    throw 'Inno Setup 6.3 or newer (ISCC.exe) was not found. Run eng/verify-tools.ps1 for setup guidance.'
}

$applicationPath = Join-Path $resolvedPublishDirectory 'YahooMonthPrint.App.exe'
if (-not (Test-Path -LiteralPath $applicationPath -PathType Leaf)) {
    throw "Published application was not found: $applicationPath"
}

$signingRequested = -not [string]::IsNullOrWhiteSpace($SignToolPath) -or
    -not [string]::IsNullOrWhiteSpace($SigningCertificateThumbprint)
if ($signingRequested) {
    if ([string]::IsNullOrWhiteSpace($SignToolPath) -or
        -not (Test-Path -LiteralPath $SignToolPath -PathType Leaf) -or
        [string]::IsNullOrWhiteSpace($SigningCertificateThumbprint)) {
        throw 'Signing was requested, but the signing tool or current-user certificate thumbprint is unavailable.'
    }

    & $SignToolPath sign /fd SHA256 /sha1 $SigningCertificateThumbprint /tr $TimestampUrl /td SHA256 $applicationPath
    if ($LASTEXITCODE -ne 0) {
        throw "Application signing failed with exit code $LASTEXITCODE."
    }
}

New-Item -ItemType Directory -Force -Path $resolvedOutputDirectory | Out-Null
& $innoCompilerPath '/Qp' "/DAppVersion=$Version" "/DPublishDir=$resolvedPublishDirectory" "/O$resolvedOutputDirectory" $installerScript
if ($LASTEXITCODE -ne 0) {
    throw "ISCC.exe failed with exit code $LASTEXITCODE."
}

$installerPath = Join-Path $resolvedOutputDirectory 'YahooMonthPrint-Setup.exe'
if (-not (Test-Path -LiteralPath $installerPath -PathType Leaf)) {
    throw "Expected installer was not produced: $installerPath"
}

if ($signingRequested) {
    & $SignToolPath sign /fd SHA256 /sha1 $SigningCertificateThumbprint /tr $TimestampUrl /td SHA256 $installerPath
    if ($LASTEXITCODE -ne 0) {
        throw "Installer signing failed with exit code $LASTEXITCODE."
    }
}

$hash = Get-FileHash -LiteralPath $installerPath -Algorithm SHA256
$checksumPath = "$installerPath.sha256"
Set-Content -LiteralPath $checksumPath -Value "$($hash.Hash.ToLowerInvariant())  $([System.IO.Path]::GetFileName($installerPath))" -Encoding ascii
Write-Host "Installer: $installerPath"
Write-Host "SHA-256: $checksumPath"
