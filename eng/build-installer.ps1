[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+(\.\d+)?$')]
    [string]$Version,

    [switch]$SkipTests,

    [string]$OutputDirectory = 'artifacts\installer',

    [string]$SignToolPath,

    [ValidatePattern('^[0-9A-Fa-f]{40}$')]
    [string]$SigningCertificateThumbprint,

    [string]$TimestampUrl = 'https://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot 'YahooMonthPrint.sln'
$applicationProject = Join-Path $repositoryRoot 'src\YahooMonthPrint.App\YahooMonthPrint.App.csproj'
$publishDirectory = 'artifacts\publish\win-x64'
$resolvedPublishDirectory = Join-Path $repositoryRoot $publishDirectory
$resolvedOutputDirectory = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
$artifactsPrefix = $artifactsRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
    [System.IO.Path]::DirectorySeparatorChar

if ([System.IO.Path]::IsPathRooted($OutputDirectory) -or
    -not $resolvedOutputDirectory.StartsWith($artifactsPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputDirectory must be a repository-relative directory under artifacts.'
}

. (Join-Path $PSScriptRoot 'InnoSetup.ps1')

if ($null -eq (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET 8 SDK is required. Install it with: winget install --id Microsoft.DotNet.SDK.8 --exact'
}

$innoCompilerPath = Resolve-InnoCompiler
if ([string]::IsNullOrWhiteSpace($innoCompilerPath)) {
    throw 'Inno Setup 6.3 or newer is required. Install it with: winget install --id JRSoftware.InnoSetup --exact --scope user'
}

$compatibility = Test-InnoCompilerCompatibility `
    -CompilerPath $innoCompilerPath `
    -InstallerScript (Join-Path $repositoryRoot 'installer\smoke\YahooMonthPrint.ToolchainSmoke.iss')
if (-not $compatibility.IsCompatible) {
    throw "Inno Setup 6.3 or newer is required. $($compatibility.Output)"
}

Push-Location $repositoryRoot
try {
    Write-Host "Building Yahoo Month Print installer version $Version"

    foreach ($artifactDirectory in @($resolvedPublishDirectory, $resolvedOutputDirectory)) {
        if (Test-Path -LiteralPath $artifactDirectory) {
            Remove-Item -LiteralPath $artifactDirectory -Recurse -Force
        }
    }

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

    $packageArguments = @{
        Version = $Version
        PublishDirectory = $publishDirectory
        OutputDirectory = $OutputDirectory
        InnoCompilerPath = $innoCompilerPath
        TimestampUrl = $TimestampUrl
    }
    if (-not [string]::IsNullOrWhiteSpace($SignToolPath)) {
        $packageArguments.SignToolPath = $SignToolPath
    }
    if (-not [string]::IsNullOrWhiteSpace($SigningCertificateThumbprint)) {
        $packageArguments.SigningCertificateThumbprint = $SigningCertificateThumbprint
    }

    & (Join-Path $PSScriptRoot 'package-release.ps1') @packageArguments
}
finally {
    Pop-Location
}
