[CmdletBinding()]
param(
    [string] $OutputDirectory = 'artifacts/system-font-verification'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runRoot = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
    [IO.Path]::GetFullPath($OutputDirectory)
}
else {
    [IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
}
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null

$platform = if ($PSVersionTable.PSEdition -eq 'Core') {
    if ($IsWindows) { 'windows' } elseif ($IsLinux) { 'linux' } elseif ($IsMacOS) { 'macos' } else { 'unknown' }
}
elseif ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) {
    'windows'
}
else {
    'unix'
}

function Test-FontFile([string] $path) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required system font file was not found: $path"
    }
    $stream = [IO.File]::OpenRead($path)
    try {
        $header = New-Object byte[] 4
        $read = $stream.Read($header, 0, 4)
        if ($read -ne 4) { throw "System font file is too short: $path" }
        $isTrueType = $header[0] -eq 0 -and $header[1] -eq 1 -and $header[2] -eq 0 -and $header[3] -eq 0
        $isOpenType = $header[0] -eq 0x4F -and $header[1] -eq 0x54 -and $header[2] -eq 0x54 -and $header[3] -eq 0x4F
        $isCollection = $header[0] -eq 0x74 -and $header[1] -eq 0x74 -and $header[2] -eq 0x63 -and $header[3] -eq 0x66
        if (-not ($isTrueType -or $isOpenType -or $isCollection)) {
            throw "System font file has an unsupported signature: $path"
        }
    }
    finally {
        $stream.Dispose()
    }
    return [pscustomobject]@{ path = $path; length = (Get-Item -LiteralPath $path).Length }
}

$checks = [Collections.Generic.List[object]]::new()
if ($platform -eq 'windows') {
    $fontRoot = Join-Path $env:WINDIR 'Fonts'
    $regular = Test-FontFile (Join-Path $fontRoot 'arial.ttf')
    $bold = Test-FontFile (Join-Path $fontRoot 'arialbd.ttf')
    $cjkCandidates = @('msyh.ttc', 'msyhbd.ttc', 'simsun.ttc', 'simhei.ttf') |
        ForEach-Object { Join-Path $fontRoot $_ } |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf }
    if (-not $cjkCandidates) {
        throw "No CJK system font was found under $fontRoot. Install Microsoft YaHei, SimSun, or SimHei before running the full suite."
    }
    $checks.Add([pscustomobject]@{ role = 'regular'; source = $regular.path; length = $regular.length })
    $checks.Add([pscustomobject]@{ role = 'bold'; source = $bold.path; length = $bold.length })
    foreach ($cjk in $cjkCandidates) {
        $font = Test-FontFile $cjk
        $checks.Add([pscustomobject]@{ role = 'cjk'; source = $font.path; length = $font.length })
    }
}
elseif ($platform -in @('linux', 'macos', 'unix')) {
    $fontMatch = Get-Command fc-match -ErrorAction SilentlyContinue
    if ($null -eq $fontMatch) {
        throw 'fc-match was not found. Install fontconfig and the required system font packages before running the full suite.'
    }
    $expectations = @(
        [pscustomobject]@{ role = 'regular'; query = 'DejaVu Sans'; expected = 'DejaVu Sans';
            style = $null; excludedStyle = 'Bold|Oblique|Italic' },
        [pscustomobject]@{ role = 'bold'; query = 'DejaVu Sans:bold'; expected = 'DejaVu Sans';
            style = 'Bold'; excludedStyle = $null },
        [pscustomobject]@{ role = 'cjk'; query = 'Noto Sans CJK SC'; expected = 'Noto Sans CJK';
            style = $null; excludedStyle = $null }
    )
    foreach ($expectation in $expectations) {
        $output = & $fontMatch.Source -f '%{family}|%{style}|%{file}\n' $expectation.query 2>&1
        $text = ($output | ForEach-Object { $_.ToString() }) -join ''
        if ([string]::IsNullOrWhiteSpace($text) -or $text -notmatch [regex]::Escape($expectation.expected)) {
            throw "fc-match could not resolve $($expectation.role) font '$($expectation.query)': $text"
        }
        if ($expectation.style -and $text -notmatch [regex]::Escape($expectation.style)) {
            throw "fc-match resolved the wrong $($expectation.role) style for '$($expectation.query)': $text"
        }
        if ($expectation.excludedStyle -and $text -match $expectation.excludedStyle) {
            throw "fc-match resolved a non-regular style for '$($expectation.query)': $text"
        }
        $checks.Add([pscustomobject]@{ role = $expectation.role; query = $expectation.query; match = $text.Trim() })
    }
}
else {
    throw "System font verification is not defined for platform '$platform'."
}

$evidence = [ordered]@{
    status = 'PASS'
    platform = $platform
    os = [Environment]::OSVersion.VersionString
    powershell = $PSVersionTable.PSVersion.ToString()
    checks = $checks.ToArray()
}
$evidencePath = Join-Path $runRoot 'system-font-verification.json'
[IO.File]::WriteAllText($evidencePath, ($evidence | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
Write-Output "System font verification PASS: $evidencePath"
