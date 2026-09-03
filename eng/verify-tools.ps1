[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$toolFailures = [System.Collections.Generic.List[string]]::new()

function Find-InnoCompiler {
    $innoCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($null -ne $innoCommand) {
        return $innoCommand.Source
    }

    $innoCandidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
        'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
        'C:\Program Files\Inno Setup 6\ISCC.exe'
    )

    return $innoCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

function Require-Command {
    param([Parameter(Mandatory)][string]$Name)

    $requiredCommand = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $requiredCommand) {
        $toolFailures.Add("Required command '$Name' was not found.")
        return
    }

    Write-Host "[ok] $Name -> $($requiredCommand.Source)"
}

Require-Command -Name git
Require-Command -Name gh
Require-Command -Name dotnet

$dotnetSdks = & dotnet --list-sdks
$dotnetSdkText = $dotnetSdks -join "`n"
if ($dotnetSdkText -notmatch '(?m)^8\.0\.') {
    $toolFailures.Add('A .NET 8 SDK was not found.')
}
else {
    Write-Host "[ok] .NET 8 SDK -> $($dotnetSdks -join ', ')"
}

$selectedDotnetSdk = & dotnet --version
if ($selectedDotnetSdk -notmatch '^8\.0\.4\d{2}$') {
    $toolFailures.Add("global.json did not select a compatible .NET 8.0.4xx SDK (selected: $selectedDotnetSdk).")
}
else {
    Write-Host "[ok] global.json selected SDK -> $selectedDotnetSdk"
}

$dotnetRuntimes = & dotnet --list-runtimes
$dotnetRuntimeText = $dotnetRuntimes -join "`n"
if ($dotnetRuntimeText -notmatch '(?m)^Microsoft\.WindowsDesktop\.App 8\.0\.') {
    $toolFailures.Add('The .NET 8 Windows Desktop runtime/targeting support was not found.')
}
else {
    Write-Host '[ok] .NET 8 Windows Desktop runtime is installed.'
}

$innoCompilerPath = Find-InnoCompiler
if ([string]::IsNullOrWhiteSpace($innoCompilerPath)) {
    $toolFailures.Add('Inno Setup 6 compiler (ISCC.exe) was not found.')
}
else {
    Write-Host "[ok] Inno Setup compiler -> $innoCompilerPath"
}

$signToolPath = Get-ChildItem -Path 'C:\Program Files (x86)\Windows Kits\10\bin' -Filter signtool.exe -Recurse -ErrorAction SilentlyContinue |
    Where-Object FullName -Match '\\x64\\signtool\.exe$' |
    Sort-Object FullName -Descending |
    Select-Object -First 1 -ExpandProperty FullName
if ($null -eq $signToolPath) {
    Write-Warning 'Optional Authenticode tool signtool.exe was not found.'
}
else {
    Write-Host "[optional] Authenticode signing tool -> $signToolPath"
}

$visualStudioPath = $null
$visualStudioLocator = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
if (Test-Path -LiteralPath $visualStudioLocator) {
    $visualStudioPath = & $visualStudioLocator -latest -products * -property installationPath
}
if ([string]::IsNullOrWhiteSpace($visualStudioPath)) {
    Write-Host '[optional] Visual Studio is not installed; the supported .NET CLI build remains available.'
}
else {
    Write-Host "[optional] Visual Studio -> $visualStudioPath"
}

$configuredPrinters = @(Get-Printer -ErrorAction SilentlyContinue)
if ($configuredPrinters.Count -eq 0) {
    Write-Warning 'No printer is configured. Add a physical or PDF printer before Phase 4 manual acceptance.'
}
else {
    Write-Host "[manual] Configured printers -> $($configuredPrinters.Name -join ', ')"
}

if ($toolFailures.Count -gt 0) {
    $toolFailures | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host 'Required Phase 1 tools are available.'
