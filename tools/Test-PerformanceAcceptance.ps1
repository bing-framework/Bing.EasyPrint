[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Baseline,
    [Parameter(Mandatory)][string] $Candidate,
    [Parameter(Mandatory)][string] $OutputPath,
    [string] $Contract = 'tools/performance-acceptance.json',
    [string] $ApprovalFile,
    [string] $BuildManifest
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (Test-Path -LiteralPath $OutputPath) { throw 'Refusing to overwrite performance evidence.' }
$spec = Get-Content -LiteralPath $Contract -Raw | ConvertFrom-Json
$before = Get-Content -LiteralPath $Baseline -Raw | ConvertFrom-Json
$after = Get-Content -LiteralPath $Candidate -Raw | ConvertFrom-Json
$contractHash = (Get-FileHash -LiteralPath $Contract -Algorithm SHA256).Hash
$failures = [Collections.Generic.List[string]]::new()
$rows = [Collections.Generic.List[object]]::new()
$approved = $false
$build = if ($BuildManifest) { Get-Content -LiteralPath $BuildManifest -Raw | ConvertFrom-Json } else { $null }
if (!$build) { $failures.Add('A source-bound build manifest is required.') }
if ($ApprovalFile) {
    $approval = Get-Content -LiteralPath $ApprovalFile -Raw | ConvertFrom-Json
    $approved = $approval.approved -eq $true -and $approval.contractSha256 -eq $contractHash -and
        $approval.authority -eq 'human-user' -and -not [string]::IsNullOrWhiteSpace($approval.userInstruction)
}
foreach ($field in @('contractSha256','harnessSha256','runtime','os','architecture','processorCount','machineHash','buildConfiguration')) {
    if ($before.$field -cne $after.$field) { $failures.Add("Environment/harness mismatch: $field") }
}
foreach ($run in @($before,$after)) {
    if ($build) {
        $expected = if ($run -eq $before) { $build.baseline } else { $build.candidate }
        foreach ($field in @('kind','commit','hash')) {
            if ($run.sourceIdentity.$field -cne $expected.sourceIdentity.$field) { $failures.Add("Source identity mismatch: $($run.label)/$field") }
        }
        if ($run.harnessSha256 -ne $build.harnessSha256) { $failures.Add('Build manifest harness mismatch') }
        if (@($expected.assemblies).Count -ne 3 -or @($run.assemblies).Count -ne 3) { $failures.Add('Invalid product assembly manifest') }
        foreach ($binary in $expected.assemblies) {
            $loaded = @($run.assemblies | Where-Object name -eq $binary.name)
            if ($loaded.Count -ne 1 -or $loaded[0].sha256 -ne $binary.sha256) { $failures.Add("Loaded assembly mismatch: $($run.label)/$($binary.name)") }
        }
    }
    if ($run.osPlatform -ne 'Windows' -or $run.architecture -ne 'X64' -or $run.runtime -notmatch '^\.NET 10\.' -or
        $run.buildConfiguration -ne 'Release' -or $run.elapsedSeconds -le 0 -or $run.elapsedSeconds -gt $spec.maximumRunSeconds -or
        $run.sourceIdentity.commit -notmatch '^[a-fA-F0-9]{40}$' -or $run.sourceIdentity.hash -notmatch '^[a-fA-F0-9]{64}$') {
        $failures.Add("Invalid platform/build/source identity: $($run.label)")
    }
    if ($run.contractSha256 -ne $contractHash -or $run.mode -ne 'MEASUREMENT' -or
        $run.samples -ne $spec.samples -or $run.warmups -ne $spec.warmups -or
        $run.iterationsPerSample -ne $spec.iterationsPerSample -or
        $run.requestedConcurrency -ne 1 -or $run.maxActualParallelism -ne 1 -or $run.maxQueuedRequests -ne 0 -or
        $run.peakWorkingSetBytes -gt $spec.maximumPeakWorkingSetBytes) { $failures.Add("Invalid run identity/sampling/memory: $($run.label)") }
    if (@($run.results).Count -ne @($spec.workloads).Count -or
        @($run.results.id | Select-Object -Unique).Count -ne @($spec.workloads).Count) { $failures.Add("Missing or duplicate workloads: $($run.label)") }
    foreach ($measurement in $run.results) {
        if ($measurement.correctness -ne 'PASS' -or @($measurement.milliseconds).Count -ne $spec.samples -or
            @($measurement.allocatedBytes).Count -ne $spec.samples -or @($measurement.outputBytes).Count -ne $spec.samples -or
            @($measurement.gcCollections).Count -ne $spec.samples) { $failures.Add("Invalid samples: $($run.label)/$($measurement.id)") }
        foreach ($value in $measurement.milliseconds) {
            if ([double]::IsNaN($value) -or [double]::IsInfinity($value) -or $value -le 0) { $failures.Add('Invalid latency sample') }
        }
        foreach ($value in $measurement.allocatedBytes) { if ($value -lt 0) { $failures.Add('Invalid allocation sample') } }
        foreach ($value in $measurement.outputBytes) { if ($value -le 0) { $failures.Add('Empty output sample') } }
    }
}
if ($before.sourceIdentity.kind -ne 'GIT_BASELINE' -or $before.sourceIdentity.commit -ne $spec.baselineCommit -or
    $after.sourceIdentity.kind -ne 'LOCAL_CANDIDATE') { $failures.Add('Baseline/candidate source provenance mismatch') }
foreach ($limit in $spec.workloads) {
    $baselineRow = @($before.results | Where-Object id -eq $limit.id)
    $candidateRow = @($after.results | Where-Object id -eq $limit.id)
    if ($baselineRow.Count -ne 1 -or $candidateRow.Count -ne 1) { $failures.Add("Missing workload: $($limit.id)"); continue }
    $b = $baselineRow[0]; $c = $candidateRow[0]
    # 从原始样本重算，避免汇总字段被篡改或采用不同统计口径。
    $times = @($c.milliseconds | Sort-Object)
    $baselineTimes = @($b.milliseconds | Sort-Object)
    $allocations = @($c.allocatedBytes | Sort-Object)
    $baselineAllocations = @($b.allocatedBytes | Sort-Object)
    if ($times.Count -ne $spec.samples -or $baselineTimes.Count -ne $spec.samples -or
        $allocations.Count -ne $spec.samples -or $baselineAllocations.Count -ne $spec.samples) { continue }
    $n = $times.Count; $lo = [int][Math]::Floor(($n - 1) / 2); $hi = [int][Math]::Floor($n / 2)
    $median = ($times[$lo] + $times[$hi]) / 2
    $p95 = $times[[int][Math]::Ceiling($n * 0.95) - 1]
    $allocated = ($allocations[$lo] + $allocations[$hi]) / 2
    $baselineMedian = ($baselineTimes[$lo] + $baselineTimes[$hi]) / 2
    $baselineAllocated = ($baselineAllocations[$lo] + $baselineAllocations[$hi]) / 2
    $pass = $median -le $limit.maximumMedianMilliseconds -and $p95 -le $limit.maximumP95Milliseconds -and
        $allocated -le $limit.maximumAllocatedBytesPerOperation -and
        $median -le ($baselineMedian * $spec.maximumMedianRegressionRatio + $spec.medianNoiseAllowanceMilliseconds) -and
        $allocated -le ($baselineAllocated * $spec.maximumAllocationRegressionRatio + $spec.allocationNoiseAllowanceBytes)
    if (-not $pass) { $failures.Add("Threshold exceeded: $($limit.id)") }
    $rows.Add([pscustomobject]@{ id=$limit.id; pass=$pass; medianMilliseconds=$median; p95Milliseconds=$p95;
        medianAllocatedBytes=$allocated; baselineMedianMilliseconds=$baselineMedian; baselineAllocatedBytes=$baselineAllocated;
        operationsPerSecond=$n * 1000 / ($times | Measure-Object -Sum).Sum })
}
$result = [ordered]@{
    status = if ($failures.Count) { 'FAIL' } elseif (!$approved) { 'BLOCKED_APPROVAL' } else { 'PASS' }
    approvalVerified=$approved; contractSha256=$contractHash; contract=$Contract
    buildManifest=if ($BuildManifest) { [ordered]@{path=$BuildManifest;sha256=(Get-FileHash -LiteralPath $BuildManifest).Hash} } else { $null }
    baseline=[ordered]@{path=$Baseline;sha256=(Get-FileHash -LiteralPath $Baseline).Hash}
    candidate=[ordered]@{path=$Candidate;sha256=(Get-FileHash -LiteralPath $Candidate).Hash}
    failures=$failures.ToArray(); results=$rows.ToArray(); evaluatedAt=[DateTimeOffset]::Now.ToString('o')
}
[void][IO.Directory]::CreateDirectory((Split-Path ([IO.Path]::GetFullPath($OutputPath)) -Parent))
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding utf8
$result | ConvertTo-Json -Depth 8
if ($result.status -eq 'FAIL') { exit 1 }
if ($result.status -eq 'BLOCKED_APPROVAL') { exit 2 }
exit 0
