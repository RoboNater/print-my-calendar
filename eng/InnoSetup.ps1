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

function Test-InnoCompilerCompatibility {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$CompilerPath,
        [Parameter(Mandatory)][string]$InstallerScript
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $CompilerPath
    $startInfo.Arguments = "/Qp /O- `"$InstallerScript`""
    $startInfo.CreateNoWindow = $true
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    $compilerProcess = [System.Diagnostics.Process]::Start($startInfo)
    $standardOutput = $compilerProcess.StandardOutput.ReadToEnd()
    $standardError = $compilerProcess.StandardError.ReadToEnd()
    $compilerProcess.WaitForExit()
    $exitCode = $compilerProcess.ExitCode
    $compilerProcess.Dispose()

    return [PSCustomObject]@{
        IsCompatible = $exitCode -eq 0
        Output = "$standardOutput`n$standardError".Trim()
    }
}

function Test-InnoInstallDirectoryName {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$DirectoryName)

    return $DirectoryName -match '^Inno Setup \d+$'
}

function Test-InnoCompilerBanner {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$Banner)

    return $Banner -match 'Inno Setup \d+ Command-Line Compiler'
}

function Resolve-InnoCompiler {
    [CmdletBinding()]
    param()

    $innoCandidates = [System.Collections.Generic.List[string]]::new()
    $innoCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($null -ne $innoCommand) {
        $innoCandidates.Add($innoCommand.Source)
    }

    $innoSearchRoots = [System.Collections.Generic.List[string]]::new()
    if (-not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        $innoSearchRoots.Add((Join-Path $env:LOCALAPPDATA 'Programs'))
    }
    if (-not [string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) {
        $innoSearchRoots.Add(${env:ProgramFiles(x86)})
    }
    if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        $innoSearchRoots.Add($env:ProgramFiles)
    }

    foreach ($innoSearchRoot in ($innoSearchRoots | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $innoSearchRoot -PathType Container)) {
            continue
        }

        $versionedInstallDirectories = Get-ChildItem -LiteralPath $innoSearchRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object { Test-InnoInstallDirectoryName -DirectoryName $_.Name } |
            Sort-Object { [int]($_.Name -replace '^Inno Setup ', '') } -Descending
        foreach ($installDirectory in $versionedInstallDirectories) {
            $innoCandidates.Add((Join-Path $installDirectory.FullName 'ISCC.exe'))
        }
    }

    foreach ($innoCandidate in ($innoCandidates | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $innoCandidate -PathType Leaf)) {
            continue
        }

        $innoBanner = Get-InnoCompilerBanner -CompilerPath $innoCandidate
        if (Test-InnoCompilerBanner -Banner $innoBanner) {
            return $innoCandidate
        }
    }

    return $null
}
