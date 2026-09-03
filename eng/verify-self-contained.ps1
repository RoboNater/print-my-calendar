[CmdletBinding()]
param(
    [string]$PublishDirectory = 'artifacts\publish\win-x64'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resolvedPublishDirectory = (Resolve-Path -LiteralPath (Join-Path $repositoryRoot $PublishDirectory)).Path
$executablePath = Join-Path $resolvedPublishDirectory 'YahooMonthPrint.App.exe'
$tracePath = Join-Path $resolvedPublishDirectory 'corehost-trace.txt'
$missingDotnetRoot = Join-Path $resolvedPublishDirectory 'missing-dotnet-root'

if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "Published application was not found: $executablePath"
}

if (Test-Path -LiteralPath $missingDotnetRoot) {
    throw "The deliberately missing DOTNET_ROOT path unexpectedly exists: $missingDotnetRoot"
}

Remove-Item -LiteralPath $tracePath -Force -ErrorAction SilentlyContinue

$originalEnvironment = @{
    COREHOST_TRACE = $env:COREHOST_TRACE
    COREHOST_TRACEFILE = $env:COREHOST_TRACEFILE
    DOTNET_ROOT = $env:DOTNET_ROOT
    DOTNET_MULTILEVEL_LOOKUP = $env:DOTNET_MULTILEVEL_LOOKUP
}

try {
    $env:COREHOST_TRACE = '1'
    $env:COREHOST_TRACEFILE = $tracePath
    $env:DOTNET_ROOT = $missingDotnetRoot
    $env:DOTNET_MULTILEVEL_LOOKUP = '0'

    $process = Start-Process `
        -FilePath $executablePath `
        -ArgumentList '--smoke-test' `
        -Wait `
        -PassThru `
        -WindowStyle Hidden
} finally {
    foreach ($name in $originalEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $originalEnvironment[$name], 'Process')
    }
}

if ($process.ExitCode -ne 0) {
    throw "Published application smoke test failed with exit code $($process.ExitCode)."
}

if (-not (Test-Path -LiteralPath $tracePath -PathType Leaf)) {
    throw "The .NET host did not create the expected trace: $tracePath"
}

$trace = Get-Content -LiteralPath $tracePath -Raw
$requiredEvidence = @(
    'Detected Single-File app bundle',
    'Using internal fxr',
    'Executing as a self-contained app as per config file',
    'Using internal hostpolicy',
    'DOTNET_MULTILEVEL_LOOKUP is set to 0',
    "Using dotnet root path [$resolvedPublishDirectory\]"
)

foreach ($evidence in $requiredEvidence) {
    if ($trace.IndexOf($evidence, [StringComparison]::Ordinal) -lt 0) {
        throw "The .NET host trace did not contain required evidence: $evidence"
    }
}

Write-Host 'Self-contained startup validated with machine-wide runtime lookup disabled.'
Write-Host "Host-resolution evidence: $tracePath"
