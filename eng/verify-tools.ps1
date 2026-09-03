[CmdletBinding()]
param(
    [switch]$BuildToolsOnly
)

$ErrorActionPreference = 'Stop'
$toolFailures = [System.Collections.Generic.List[string]]::new()
. (Join-Path $PSScriptRoot 'InnoSetup.ps1')

function Require-Command {
    param([Parameter(Mandatory)][string]$Name)

    $requiredCommand = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $requiredCommand) {
        $toolFailures.Add("Required command '$Name' was not found.")
        return $null
    }

    Write-Host "[ok] $Name -> $($requiredCommand.Source)"
    return $requiredCommand
}

$gitCommand = Require-Command -Name git
$dotnetCommand = Require-Command -Name dotnet

if ($null -ne $dotnetCommand) {
    $dotnetSdks = & $dotnetCommand.Source --list-sdks
    $dotnetSdkText = $dotnetSdks -join "`n"
    if ($dotnetSdkText -notmatch '(?m)^8\.0\.') {
        $toolFailures.Add('A .NET 8 SDK was not found.')
    }
    else {
        Write-Host "[ok] .NET 8 SDK -> $($dotnetSdks -join ', ')"
    }

    $selectedDotnetSdk = & $dotnetCommand.Source --version
    if ($selectedDotnetSdk -notmatch '^8\.0\.4\d{2}$') {
        $toolFailures.Add("global.json did not select a compatible .NET 8.0.4xx SDK (selected: $selectedDotnetSdk).")
    }
    else {
        Write-Host "[ok] global.json selected SDK -> $selectedDotnetSdk"
    }

    $dotnetRuntimes = & $dotnetCommand.Source --list-runtimes
    $dotnetRuntimeText = $dotnetRuntimes -join "`n"
    if ($dotnetRuntimeText -notmatch '(?m)^Microsoft\.WindowsDesktop\.App 8\.0\.') {
        $toolFailures.Add('The .NET 8 Windows Desktop runtime/targeting support was not found.')
    }
    else {
        Write-Host '[ok] .NET 8 Windows Desktop runtime is installed.'
    }
}

if (-not $BuildToolsOnly) {
    $githubCliCommand = Require-Command -Name gh
    if ($null -ne $githubCliCommand) {
        & $githubCliCommand.Source auth token *> $null
        if ($LASTEXITCODE -ne 0) {
            $toolFailures.Add("GitHub CLI is not authenticated. Run 'gh auth login'.")
        }
        else {
            Write-Host '[delivery] GitHub CLI is authenticated.'
        }
    }

    $innoCompilerPath = Resolve-InnoCompiler
    if ([string]::IsNullOrWhiteSpace($innoCompilerPath)) {
        $toolFailures.Add("Inno Setup $script:MinimumInnoSetupVersion or newer was not found.")
    }
    else {
        $innoCompilerVersion = Get-InnoCompilerVersion -CompilerPath $innoCompilerPath
        Write-Host "[packaging] Inno Setup $innoCompilerVersion -> $innoCompilerPath"
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
}

if ($toolFailures.Count -gt 0) {
    $toolFailures | ForEach-Object { Write-Error $_ }
    exit 1
}

if ($BuildToolsOnly) {
    Write-Host 'Required build tools are available.'
}
else {
    Write-Host 'Required Phase 1 build, PR-delivery, and packaging tools are available.'
}
