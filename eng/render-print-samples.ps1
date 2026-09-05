[CmdletBinding()]
param(
    [string]$ExecutablePath = 'artifacts\publish\win-x64\YahooMonthPrint.App.exe',
    [string]$OutputDirectory = 'artifacts\print-samples'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resolvedExecutable = (Resolve-Path -LiteralPath (Join-Path $repositoryRoot $ExecutablePath)).Path
$resolvedOutput = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
if (Test-Path -LiteralPath $resolvedOutput -PathType Container) {
    Get-ChildItem -LiteralPath $resolvedOutput -Filter 'september-2026-*-page-*.png' -File |
        Remove-Item -Force
}

$process = Start-Process `
    -FilePath $resolvedExecutable `
    -ArgumentList @('--render-print-samples', "`"$resolvedOutput`"") `
    -Wait `
    -PassThru `
    -WindowStyle Hidden
if ($process.ExitCode -ne 0) {
    throw "Print sample rendering failed with exit code $($process.ExitCode)."
}

$letterSample = Join-Path $resolvedOutput 'september-2026-letter-page-1.png'
$a4Sample = Join-Path $resolvedOutput 'september-2026-a4-page-1.png'
foreach ($sample in @($letterSample, $a4Sample)) {
    if (-not (Test-Path -LiteralPath $sample -PathType Leaf) -or
        (Get-Item -LiteralPath $sample).Length -eq 0) {
        throw "Expected print sample was not produced: $sample"
    }
}

Write-Host "Print samples: $resolvedOutput"
