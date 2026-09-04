[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'InnoSetup.ps1')

foreach ($supportedName in @('Inno Setup 6', 'Inno Setup 7', 'Inno Setup 12')) {
    if (-not (Test-InnoInstallDirectoryName -DirectoryName $supportedName)) {
        throw "Expected a numeric Inno Setup directory to be discoverable: $supportedName"
    }
}

foreach ($unsupportedName in @('Inno Setup', 'Inno Setup Preview', 'Inno Setup six')) {
    if (Test-InnoInstallDirectoryName -DirectoryName $unsupportedName) {
        throw "Expected a non-numeric Inno Setup directory to be ignored: $unsupportedName"
    }
}

foreach ($supportedBanner in @(
    'Inno Setup 6 Command-Line Compiler',
    'Inno Setup 7 Command-Line Compiler',
    'Inno Setup 12 Command-Line Compiler')) {
    if (-not (Test-InnoCompilerBanner -Banner $supportedBanner)) {
        throw "Expected a numeric Inno Setup compiler banner to be accepted: $supportedBanner"
    }
}

if (Test-InnoCompilerBanner -Banner 'An unrelated compiler') {
    throw 'Expected an unrelated compiler banner to be rejected.'
}

Write-Host 'Inno Setup discovery accepts numeric major versions.'
