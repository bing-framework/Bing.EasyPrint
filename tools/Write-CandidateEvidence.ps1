[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $OutputPath,
    [string] $ExpectedCommit,
    [string] $ExpectedScopeHash,
    [switch] $AllowDirty
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$utf8 = [Text.UTF8Encoding]::new($false)

function Invoke-Git([string[]] $Arguments) {
    $output = @(& git -C $repositoryRoot @Arguments 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed: $($output -join [Environment]::NewLine)"
    }
    return $output
}

function ConvertTo-RepositoryPath([string] $Path) {
    return $Path.Replace('\', '/')
}

function Test-ExcludedPath([string] $Path) {
    return $Path -match '(^|/)(artifacts|bin|obj|node_modules|dist|test-results)(/|$)'
}

function Test-GeneratedDocumentation([string] $Path) {
    return $Path -ceq 'src/Bing.EasyPrint/Bing.EasyPrint.xml' -or
        $Path -ceq 'src/Bing.EasyPrint.Templates/Bing.EasyPrint.Templates.xml'
}

function Test-SourcePath([string] $Path) {
    if (Test-ExcludedPath $Path) { return $false }
    if (Test-GeneratedDocumentation $Path) { return $false }
    if ($Path.StartsWith('src/', [StringComparison]::Ordinal) -or
        $Path.StartsWith('tests/', [StringComparison]::Ordinal) -or
        $Path.StartsWith('tools/', [StringComparison]::Ordinal) -or
        $Path.StartsWith('web/', [StringComparison]::Ordinal)) {
        return $true
    }
    return $Path -match '^[^/]+\.(props|csproj|sln)$'
}

function Test-NonSourceDocumentation([string] $Path) {
    return $Path.StartsWith('docs/', [StringComparison]::Ordinal) -or
        $Path.StartsWith('ai_docs/', [StringComparison]::Ordinal) -or
        (Test-GeneratedDocumentation $Path)
}

function Get-PorcelainEntries {
    $entries = [Collections.Generic.List[object]]::new()
    foreach ($line in Invoke-Git @('status', '--porcelain=v1', '--untracked-files=all', '--no-renames')) {
        $record = [string] $line
        if ($record.Length -lt 4) { continue }
        $path = ConvertTo-RepositoryPath $record.Substring(3)
        $entries.Add([pscustomobject]@{
            status = $record.Substring(0, 2)
            path = $path
        })
    }
    return $entries.ToArray()
}

function Get-IndexEntries {
    $entries = [Collections.Generic.List[object]]::new()
    foreach ($line in Invoke-Git @('ls-files', '--stage')) {
        $record = [string] $line
        $tab = $record.IndexOf("`t", [StringComparison]::Ordinal)
        if ($tab -lt 0) { throw "Cannot parse git ls-files entry: $record" }
        $header = $record.Substring(0, $tab)
        $path = ConvertTo-RepositoryPath $record.Substring($tab + 1)
        if ($header -notmatch '^(?<mode>[0-7]{6}) (?<blob>[0-9a-fA-F]{40,64}) (?<stage>[0-3])$') {
            throw "Cannot parse git index identity for: $path"
        }
        $entries.Add([pscustomobject]@{
            path = $path
            mode = $Matches.mode
            blob = $Matches.blob.ToLowerInvariant()
            stage = [int] $Matches.stage
        })
    }
    return $entries.ToArray()
}

function Get-CanonicalFileContent([string] $FullPath, [string] $RelativePath) {
    $bytes = [IO.File]::ReadAllBytes($FullPath)
    $binaryExtensions = @(
        '.7z', '.bin', '.bmp', '.dat', '.dll', '.exe', '.gif', '.ico', '.jpeg', '.jpg',
        '.otf', '.pdf', '.png', '.pfx', '.tif', '.tiff', '.ttf', '.woff', '.woff2', '.zip'
    )
    $extension = [IO.Path]::GetExtension($RelativePath).ToLowerInvariant()
    if ($binaryExtensions -contains $extension -or [Array]::IndexOf($bytes, [byte] 0) -ge 0) {
        return [pscustomobject]@{ bytes = $bytes; normalizedLineEndings = $false }
    }

    $stream = [IO.MemoryStream]::new($bytes.Length)
    try {
        for ($index = 0; $index -lt $bytes.Length; $index++) {
            if ($bytes[$index] -eq 13 -and $index + 1 -lt $bytes.Length -and $bytes[$index + 1] -eq 10) {
                $stream.WriteByte(10)
                $index++
            }
            else {
                $stream.WriteByte($bytes[$index])
            }
        }
        return [pscustomobject]@{ bytes = $stream.ToArray(); normalizedLineEndings = $true }
    }
    finally {
        $stream.Dispose()
    }
}

function Get-FileIdentity([string] $RelativePath, $IndexEntry) {
    $fullPath = Join-Path $repositoryRoot ($RelativePath.Replace('/', [IO.Path]::DirectorySeparatorChar))
    $exists = Test-Path -LiteralPath $fullPath -PathType Leaf
    if (-not $exists) {
        return [pscustomobject]@{
            identity = [pscustomobject]@{
                path = $RelativePath
                tracked = $null -ne $IndexEntry
                gitMode = if ($IndexEntry) { $IndexEntry.mode } else { $null }
                gitBlob = if ($IndexEntry) { $IndexEntry.blob } else { $null }
                stage = if ($IndexEntry) { $IndexEntry.stage } else { $null }
                fileSha256 = $null
                sizeBytes = $null
                normalizedLineEndings = $null
                missingFromWorktree = $true
            }
            canonicalBytes = $null
        }
    }

    $content = Get-CanonicalFileContent $fullPath $RelativePath
    $fileHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($content.bytes)).ToLowerInvariant()
    return [pscustomobject]@{
        identity = [pscustomobject]@{
            path = $RelativePath
            tracked = $null -ne $IndexEntry
            gitMode = if ($IndexEntry) { $IndexEntry.mode } else { $null }
            gitBlob = if ($IndexEntry) { $IndexEntry.blob } else { $null }
            stage = if ($IndexEntry) { $IndexEntry.stage } else { $null }
            fileSha256 = $fileHash
            sizeBytes = [IO.FileInfo]::new($fullPath).Length
            normalizedLineEndings = $content.normalizedLineEndings
            missingFromWorktree = $false
        }
        canonicalBytes = $content.bytes
    }
}

function Get-SourceSnapshot($IndexEntries, $StatusEntries) {
    $indexByPath = @{}
    foreach ($entry in $IndexEntries) { $indexByPath[$entry.path] = $entry }

    $pathSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($entry in $IndexEntries) {
        if (Test-SourcePath $entry.path) { [void] $pathSet.Add($entry.path) }
    }
    foreach ($entry in $StatusEntries) {
        if (Test-SourcePath $entry.path) { [void] $pathSet.Add($entry.path) }
    }

    $paths = [Collections.Generic.List[string]]::new()
    foreach ($path in $pathSet) { $paths.Add($path) }
    $paths.Sort([StringComparer]::Ordinal)

    $fileIdentities = [Collections.Generic.List[object]]::new()
    $scopeHash = [Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256)
    try {
        foreach ($path in $paths) {
            $indexEntry = if ($indexByPath.ContainsKey($path)) { $indexByPath[$path] } else { $null }
            $fullPath = Join-Path $repositoryRoot ($path.Replace('/', [IO.Path]::DirectorySeparatorChar))
            $fileContent = Get-FileIdentity $path $indexEntry
            $fileIdentities.Add($fileContent.identity)
            if ($fileContent.identity.missingFromWorktree) { continue }

            $scopeHash.AppendData($utf8.GetBytes($path + "`n"))
            $scopeHash.AppendData($fileContent.canonicalBytes)
            $scopeHash.AppendData([byte[]] @(10))
        }
        $digest = [Convert]::ToHexString($scopeHash.GetHashAndReset()).ToLowerInvariant()
    }
    finally {
        $scopeHash.Dispose()
    }

    return [pscustomobject]@{ hash = $digest; files = $fileIdentities.ToArray() }
}

$outputFullPath = if ([IO.Path]::IsPathRooted($OutputPath)) {
    [IO.Path]::GetFullPath($OutputPath)
}
else {
    [IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputPath))
}
if (Test-Path -LiteralPath $outputFullPath) {
    throw "Candidate evidence output already exists and will not be overwritten: $outputFullPath"
}
if (-not $AllowDirty -and [string]::IsNullOrWhiteSpace($ExpectedCommit)) {
    throw 'ExpectedCommit is required unless -AllowDirty is used for a local preflight.'
}

$head = ((Invoke-Git @('rev-parse', 'HEAD')) -join '').Trim()
$tree = ((Invoke-Git @('rev-parse', 'HEAD^{tree}')) -join '').Trim()
$indexBefore = @(Get-IndexEntries)
$statusBefore = @(Get-PorcelainEntries)
$snapshot = Get-SourceSnapshot $indexBefore $statusBefore
$statusAfter = @(Get-PorcelainEntries)
$indexAfter = @(Get-IndexEntries)

$statusBeforeJson = ConvertTo-Json -InputObject @($statusBefore | Sort-Object path,status) -Compress -Depth 4
$statusAfterJson = ConvertTo-Json -InputObject @($statusAfter | Sort-Object path,status) -Compress -Depth 4
$indexBeforeJson = ConvertTo-Json -InputObject @($indexBefore | Sort-Object path,stage) -Compress -Depth 4
$indexAfterJson = ConvertTo-Json -InputObject @($indexAfter | Sort-Object path,stage) -Compress -Depth 4
$snapshotStable = $statusBeforeJson -ceq $statusAfterJson -and $indexBeforeJson -ceq $indexAfterJson

$sourceDirty = @($statusAfter | Where-Object { Test-SourcePath $_.path })
$nonSourceDirty = @($statusAfter | Where-Object { -not (Test-ExcludedPath $_.path) -and (Test-NonSourceDocumentation $_.path) })
$otherDirty = @($statusAfter | Where-Object {
    -not (Test-ExcludedPath $_.path) -and
    -not (Test-SourcePath $_.path) -and
    -not (Test-NonSourceDocumentation $_.path)
})
$excludedGenerated = @($statusAfter | Where-Object { Test-ExcludedPath $_.path })

$failureReasons = [Collections.Generic.List[string]]::new()
if ($ExpectedCommit -and $head -ine $ExpectedCommit.Trim()) {
    $failureReasons.Add("HEAD '$head' does not match expected commit '$($ExpectedCommit.Trim())'.")
}
if ($sourceDirty.Count -gt 0 -and -not $AllowDirty) {
    $failureReasons.Add('Tracked or untracked files in the candidate source scope are dirty.')
}
if ($otherDirty.Count -gt 0 -and -not $AllowDirty) {
    $failureReasons.Add('Files outside the source and explicitly allowed documentation scope are dirty.')
}
if (-not $snapshotStable) {
    $failureReasons.Add('Git index or worktree status changed while candidate evidence was collected.')
}
if ($ExpectedScopeHash -and $snapshot.hash -ine $ExpectedScopeHash.Trim()) {
    $failureReasons.Add("Scope hash '$($snapshot.hash)' does not match expected scope hash '$($ExpectedScopeHash.Trim())'.")
}

$status = if ($failureReasons.Count -gt 0) { 'FAIL' } elseif ($AllowDirty) { 'LOCAL_UNCOMMITTED' } else { 'PASS' }
$gitVersion = ((Invoke-Git @('--version')) -join '').Trim()
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
$sdkVersion = $null
if ($dotnetCommand) {
    $sdkOutput = @(& $dotnetCommand.Source --version 2>&1)
    if ($LASTEXITCODE -eq 0) { $sdkVersion = ($sdkOutput -join ' ').Trim() }
}

$evidence = [ordered]@{
    schemaVersion = 1
    status = $status
    failureReasons = $failureReasons.ToArray()
    capturedAtUtc = [DateTimeOffset]::UtcNow.ToString('o')
    candidate = [ordered]@{
        head = $head
        expectedCommit = if ($ExpectedCommit) { $ExpectedCommit.Trim() } else { $null }
        tree = $tree
        scopeHash = $snapshot.hash
        scopeHashAlgorithm = 'SHA256(path UTF-8 + LF + content with CRLF normalized to LF + LF, per path in ordinal order)'
        expectedScopeHash = if ($ExpectedScopeHash) { $ExpectedScopeHash.Trim().ToLowerInvariant() } else { $null }
        sourceFileCount = $snapshot.files.Count
        files = $snapshot.files
    }
    worktree = [ordered]@{
        snapshotStable = $snapshotStable
        rawStatus = @($statusAfter | ForEach-Object { "$($_.status) $($_.path)" })
        sourceDirty = $sourceDirty
        nonSourceDocumentationDirty = $nonSourceDirty
        otherDirty = $otherDirty
        excludedGeneratedOrArtifactPaths = $excludedGenerated
        dirtySourceAllowedForLocalPreflight = [bool] $AllowDirty
    }
    environment = [ordered]@{
        os = [Runtime.InteropServices.RuntimeInformation]::OSDescription
        osArchitecture = [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()
        processArchitecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
        powershell = $PSVersionTable.PSVersion.ToString()
        dotnetSdk = $sdkVersion
        git = $gitVersion
        githubActions = $env:GITHUB_ACTIONS
        githubRunId = $env:GITHUB_RUN_ID
        githubRunAttempt = $env:GITHUB_RUN_ATTEMPT
        githubJob = $env:GITHUB_JOB
        githubWorkflow = $env:GITHUB_WORKFLOW
        githubRef = $env:GITHUB_REF
        githubSha = $env:GITHUB_SHA
        githubRepository = $env:GITHUB_REPOSITORY
    }
}

$parentPath = Split-Path -Parent $outputFullPath
if ($parentPath) { [void] [IO.Directory]::CreateDirectory($parentPath) }
$json = ConvertTo-Json -InputObject $evidence -Depth 12
$outputBytes = $utf8.GetBytes($json + "`n")
$fileStream = [IO.File]::Open($outputFullPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try {
    $fileStream.Write($outputBytes, 0, $outputBytes.Length)
}
finally {
    $fileStream.Dispose()
}

Write-Output "Candidate evidence ${status}: $outputFullPath"
Write-Output "HEAD: $head"
Write-Output "Tree: $tree"
Write-Output "Scope SHA-256: $($snapshot.hash) ($($snapshot.files.Count) files)"
if ($nonSourceDirty.Count -gt 0) {
    Write-Output "Non-source documentation/XML changes: $($nonSourceDirty.Count) (recorded; not part of source gate)."
}
if ($failureReasons.Count -gt 0) {
    foreach ($reason in $failureReasons) { [Console]::Error.WriteLine($reason) }
    exit 1
}
exit 0
