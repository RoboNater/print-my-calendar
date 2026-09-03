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

        $innoBanner = (& $innoCandidate /? 2>&1) -join "`n"
        if ($innoBanner -match 'Inno Setup 6 Command-Line Compiler') {
            return $innoCandidate
        }
    }

    return $null
}
