[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $BaselineDirectory,
    [Parameter(Mandatory)][string] $CandidateDirectory,
    [Parameter(Mandatory)][string] $ResultsDirectory,
    [Parameter(Mandatory)][string] $ApprovalFile,
    [Parameter(Mandatory)][string] $BaselineIdentityFile,
    [Parameter(Mandatory)][string] $CandidateIdentityFile,
    [Parameter(Mandatory)][string] $BuildManifest,
    [string] $Contract = 'tools/performance-acceptance.json'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$contractPath = (Resolve-Path -LiteralPath $Contract).Path
$approvalPath = (Resolve-Path -LiteralPath $ApprovalFile).Path
$contractHash = (Get-FileHash -LiteralPath $contractPath -Algorithm SHA256).Hash
$approval = Get-Content -LiteralPath $approvalPath -Raw | ConvertFrom-Json
if ($approval.approved -ne $true -or $approval.authority -ne 'human-user' -or
    $approval.contractSha256 -ne $contractHash -or [string]::IsNullOrWhiteSpace($approval.userInstruction)) {
    throw 'Explicit approval for this exact performance contract is required.'
}
$spec = Get-Content -LiteralPath $contractPath -Raw | ConvertFrom-Json
$baselineIdentity = Get-Content -LiteralPath $BaselineIdentityFile -Raw | ConvertFrom-Json
$candidateIdentity = Get-Content -LiteralPath $CandidateIdentityFile -Raw | ConvertFrom-Json
$build = Get-Content -LiteralPath $BuildManifest -Raw | ConvertFrom-Json
if ($baselineIdentity.commit -ne $spec.baselineCommit -or $baselineIdentity.archive -notmatch '^[a-fA-F0-9]{64}$' -or
    $candidateIdentity.status -notin @('PASS','LOCAL_UNCOMMITTED') -or
    $candidateIdentity.candidate.scopeHash -notmatch '^[a-fA-F0-9]{64}$') { throw 'Missing or invalid source identity.' }
if ($build.baseline.sourceIdentity.commit -ne $baselineIdentity.commit -or
    $build.baseline.sourceIdentity.hash -ne $baselineIdentity.archive -or
    $build.candidate.sourceIdentity.commit -ne $candidateIdentity.candidate.head -or
    $build.candidate.sourceIdentity.hash -ne $candidateIdentity.candidate.scopeHash) { throw 'Build manifest does not match source identities.' }
$resultRoot = [IO.Path]::GetFullPath($ResultsDirectory)
if (Test-Path -LiteralPath $resultRoot) { throw 'Results directory must be new; existing evidence is immutable.' }
New-Item -ItemType Directory -Path $resultRoot | Out-Null
$hostPath = (Get-Command dotnet -ErrorAction Stop).Source
$baselineAssembly = Join-Path (Resolve-Path -LiteralPath $BaselineDirectory).Path 'PreviewBundleProbe.dll'
$candidateAssembly = Join-Path (Resolve-Path -LiteralPath $CandidateDirectory).Path 'PreviewBundleProbe.dll'
if ((Get-FileHash -LiteralPath $baselineAssembly).Hash -ne (Get-FileHash -LiteralPath $candidateAssembly).Hash) {
    throw 'The baseline and candidate must execute the same harness binary.'
}
foreach ($item in @(@('baseline',$baselineAssembly), @('candidate',$candidateAssembly))) {
    $label = $item[0]
    $binaryDirectory = Split-Path -Parent $item[1]
    foreach ($binary in $build.$label.assemblies) {
        if ((Get-FileHash -LiteralPath (Join-Path $binaryDirectory ($binary.name + '.dll'))).Hash -ne $binary.sha256) {
            throw "Build manifest assembly mismatch before run: $label/$($binary.name)"
        }
    }
    $jsonPath = Join-Path $resultRoot ($label + '.json')
    $arguments = @($item[1], '--acceptance', $jsonPath, $label, $contractPath)
    $info = [Diagnostics.ProcessStartInfo]::new($hostPath)
    $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    $info.Environment['EASYPRINT_PROBE_SOURCE_KIND'] = if ($label -eq 'baseline') { 'GIT_BASELINE' } else { 'LOCAL_CANDIDATE' }
    $info.Environment['EASYPRINT_PROBE_SOURCE_COMMIT'] = if ($label -eq 'baseline') { $baselineIdentity.commit } else { $candidateIdentity.candidate.head }
    $info.Environment['EASYPRINT_PROBE_SOURCE_HASH'] = if ($label -eq 'baseline') { $baselineIdentity.archive } else { $candidateIdentity.candidate.scopeHash }
    foreach ($argument in $arguments) { $info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $info
    $started = [DateTimeOffset]::Now
    $watch = [Diagnostics.Stopwatch]::StartNew()
    [void]$process.Start()
    $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
    $timedOut = $false
    while (-not $process.WaitForExit(1000)) {
        if ($watch.Elapsed.TotalSeconds -gt $spec.maximumRunSeconds) { $timedOut = $true; $process.Kill($true); break }
    }
    $process.WaitForExit(); $watch.Stop()
    $stdout.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $resultRoot ($label + '.stdout.log')) -Encoding utf8
    $stderr.GetAwaiter().GetResult() | Set-Content -LiteralPath (Join-Path $resultRoot ($label + '.stderr.log')) -Encoding utf8
    [ordered]@{ label=$label; command=$hostPath; arguments=$arguments; pid=$process.Id;
        startedAt=$started.ToString('o'); elapsedSeconds=$watch.Elapsed.TotalSeconds;
        timeoutSeconds=$spec.maximumRunSeconds; timedOut=$timedOut; exitCode=$process.ExitCode;
        harnessSha256=(Get-FileHash -LiteralPath $item[1]).Hash; contractSha256=$contractHash
        baselineIdentitySha256=(Get-FileHash -LiteralPath $BaselineIdentityFile).Hash
        candidateIdentitySha256=(Get-FileHash -LiteralPath $CandidateIdentityFile).Hash
        buildManifestSha256=(Get-FileHash -LiteralPath $BuildManifest).Hash
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $resultRoot ($label + '.execution.json')) -Encoding utf8
    $code = $process.ExitCode; $process.Dispose()
    if ($timedOut -or $code -ne 0) { throw "Performance process failed: $label; timeout=$timedOut; exit=$code" }
    foreach ($binary in $build.$label.assemblies) {
        if ((Get-FileHash -LiteralPath (Join-Path $binaryDirectory ($binary.name + '.dll'))).Hash -ne $binary.sha256) {
            throw "Build manifest assembly mismatch after run: $label/$($binary.name)"
        }
    }
}
& (Join-Path $PSScriptRoot 'Test-PerformanceAcceptance.ps1') -Baseline (Join-Path $resultRoot 'baseline.json') -Candidate (Join-Path $resultRoot 'candidate.json') -Contract $contractPath -ApprovalFile $approvalPath -BuildManifest $BuildManifest -OutputPath (Join-Path $resultRoot 'acceptance.json')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
exit 0
