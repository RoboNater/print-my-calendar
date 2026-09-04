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
            return $innoCandidate
        }
    }

    return $null
}
