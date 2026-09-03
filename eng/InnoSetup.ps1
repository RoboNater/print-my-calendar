$script:MinimumInnoSetupVersion = [Version]'6.3.0'

function Get-InnoCompilerVersion {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$CompilerPath)

    $versionFiles = @(
        $CompilerPath,
        (Join-Path (Split-Path -Parent $CompilerPath) 'unins000.exe')
    )

    foreach ($versionFile in $versionFiles) {
        if (-not (Test-Path -LiteralPath $versionFile -PathType Leaf)) {
            continue
        }

        $productVersion = (Get-Item -LiteralPath $versionFile).VersionInfo.ProductVersion
        if ($productVersion -notmatch '\d+\.\d+(?:\.\d+){0,2}') {
            continue
        }

        try {
            $parsedVersion = [Version]$Matches[0]
            if ($parsedVersion.Major -gt 0) {
                return $parsedVersion
            }
        }
        catch {
            continue
        }
    }

    return $null
}

function Get-InnoCompilerBanner {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$CompilerPath)

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $CompilerPath
    $startInfo.Arguments = '/?'
    $startInfo.CreateNoWindow = $true
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    $compilerProcess = [System.Diagnostics.Process]::Start($startInfo)
    $standardOutput = $compilerProcess.StandardOutput.ReadToEnd()
    $standardError = $compilerProcess.StandardError.ReadToEnd()
    $compilerProcess.WaitForExit()
    $compilerProcess.Dispose()

    return "$standardOutput`n$standardError"
}

function Resolve-InnoCompiler {
    [CmdletBinding()]
    param()

    $innoCandidates = [System.Collections.Generic.List[string]]::new()
    $innoCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($null -ne $innoCommand) {
        $innoCandidates.Add($innoCommand.Source)
    }

    $innoCandidates.Add((Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'))
    $innoCandidates.Add('C:\Program Files (x86)\Inno Setup 6\ISCC.exe')
    $innoCandidates.Add('C:\Program Files\Inno Setup 6\ISCC.exe')

    foreach ($innoCandidate in ($innoCandidates | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $innoCandidate -PathType Leaf)) {
            continue
        }

        $innoBanner = Get-InnoCompilerBanner -CompilerPath $innoCandidate
        if ($innoBanner -match 'Inno Setup 6 Command-Line Compiler') {
            $innoVersion = Get-InnoCompilerVersion -CompilerPath $innoCandidate
            if ($null -eq $innoVersion) {
                Write-Warning "Could not determine the Inno Setup version for '$innoCandidate'."
                continue
            }

            if ($innoVersion -lt $script:MinimumInnoSetupVersion) {
                Write-Warning "Inno Setup $innoVersion is below the required $script:MinimumInnoSetupVersion floor."
                continue
            }

            return $innoCandidate
        }
    }

    return $null
}
