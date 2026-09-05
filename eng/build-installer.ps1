[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+(\.\d+)?$')]
    [string]$Version,

    [switch]$SkipTests,

    [string]$SignToolPath,

    [ValidatePattern('^[0-9A-Fa-f]{40}$')]
    [string]$SigningCertificateThumbprint
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot 'YahooMonthPrint.sln'
$applicationProject = Join-Path $repositoryRoot 'src\YahooMonthPrint.App\YahooMonthPrint.App.csproj'
$publishDirectory = 'artifacts\publish\win-x64'
$resolvedPublishDirectory = Join-Path $repositoryRoot $publishDirectory

. (Join-Path $PSScriptRoot 'InnoSetup.ps1')

if ($null -eq (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET 8 SDK is required. Install it with: winget install --id Microsoft.DotNet.SDK.8 --exact'
}

$innoCompilerPath = Resolve-InnoCompiler
if ([string]::IsNullOrWhiteSpace($innoCompilerPath)) {
    throw 'Inno Setup 6.3 or newer is required. Install it with: winget install --id JRSoftware.InnoSetup --exact --scope user'
}

Push-Location $repositoryRoot
try {
    Write-Host "Building Yahoo Month Print installer version $Version"

    & dotnet restore $solutionPath --locked-mode
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed with exit code $LASTEXITCODE."
    }

    & dotnet build $solutionPath --configuration Release --no-restore -p:Version=$Version
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE."
    }

    if (-not $SkipTests) {
        & dotnet test $solutionPath --configuration Release --no-build -p:Version=$Version
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet test failed with exit code $LASTEXITCODE."
        }
    }

    & dotnet publish $applicationProject `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        --no-restore `
        --output $resolvedPublishDirectory `
        -p:Version=$Version `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    & (Join-Path $PSScriptRoot 'verify-self-contained.ps1') -PublishDirectory $publishDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "Self-contained verification failed with exit code $LASTEXITCODE."
    }

    $packageArguments = @{
        Version = $Version
        PublishDirectory = $publishDirectory
    }
    if (-not [string]::IsNullOrWhiteSpace($SignToolPath)) {
        $packageArguments.SignToolPath = $SignToolPath
    }
    if (-not [string]::IsNullOrWhiteSpace($SigningCertificateThumbprint)) {
        $packageArguments.SigningCertificateThumbprint = $SigningCertificateThumbprint
    }

    & (Join-Path $PSScriptRoot 'package-release.ps1') @packageArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Installer packaging failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
