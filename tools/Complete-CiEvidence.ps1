[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('windows','linux')][string] $Platform,
    [Parameter(Mandatory)][string] $OutputDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$required = @('candidate','fonts','core','host','consumer','build','vitest','browser')
if ($Platform -eq 'windows') { $required += 'legacy' }
$failures = [Collections.Generic.List[string]]::new()
$stepResults = if ($env:CI_STEP_RESULTS) { $env:CI_STEP_RESULTS | ConvertFrom-Json } else { $null }
foreach ($step in $required) {
    $property = if ($stepResults) { $stepResults.PSObject.Properties[$step] } else { $null }
    if (!$property -or $property.Value.outcome -ne 'success') { $failures.Add("Required step did not succeed: $step") }
}
try {
    $start = Get-Content -LiteralPath (Join-Path $OutputDirectory 'candidate-start.json') -Raw | ConvertFrom-Json
    if ($start.status -ne 'PASS') { throw 'Starting candidate was not a clean CI candidate.' }
    & (Join-Path $PSScriptRoot 'Write-CandidateEvidence.ps1') -OutputPath (Join-Path $OutputDirectory 'candidate-end.json') -ExpectedCommit $env:GITHUB_SHA -ExpectedScopeHash $start.candidate.scopeHash
    if ($LASTEXITCODE -ne 0) { throw 'Ending candidate identity failed.' }
} catch { $failures.Add($_.Exception.Message) }
if ($env:GITHUB_ACTIONS -ne 'true') { $failures.Add('Not a GitHub Actions execution.') }
$report = [ordered]@{
    status=if ($failures.Count) {'FAIL'} else {'PASS'}; platform=$Platform
    commit=$env:GITHUB_SHA; runId=$env:GITHUB_RUN_ID; attempt=$env:GITHUB_RUN_ATTEMPT
    url="https://github.com/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID"
    requiredSteps=$required; steps=$stepResults; failures=$failures.ToArray()
}
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'job-result.json') -Encoding utf8
if ($failures.Count) { throw ($failures -join '; ') }
