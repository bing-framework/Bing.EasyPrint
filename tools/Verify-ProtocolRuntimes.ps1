[CmdletBinding()]
param(
    [ValidateSet('net7.0', 'net6.0', 'net5.0', 'netcoreapp3.1', 'netcoreapp2.2')]
    [string[]] $Frameworks = @('net7.0', 'net6.0', 'net5.0', 'netcoreapp3.1', 'netcoreapp2.2'),
    [string] $RuntimeDirectory = 'artifacts/test-runtimes',
    [string] $ResultsDirectory = 'artifacts/protocol-runtime-verification',
    [switch] $NoBuild,
    [switch] $NoRestore
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot 'tests/Bing.EasyPrint.Tests/Bing.EasyPrint.Tests.csproj'
$sdkHost = (Get-Command dotnet -ErrorAction Stop).Source

function Resolve-RepositoryPath([string] $path) {
    if ([IO.Path]::IsPathRooted($path)) { return [IO.Path]::GetFullPath($path) }
    return [IO.Path]::GetFullPath((Join-Path $repositoryRoot $path))
}

$runtimeRoot = Resolve-RepositoryPath $RuntimeDirectory
$resultsRoot = Resolve-RepositoryPath $ResultsDirectory
# 每次运行单独保留证据，不覆盖诊断失败或以前通过的结果。
$runPath = Join-Path $resultsRoot ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $runPath -Force | Out-Null
$portableVersions = @{ 'net7.0' = '7.0.20'; 'net5.0' = '5.0.17'; 'netcoreapp2.2' = '2.2.8' }
$runtimePrefixes = @{ 'net7.0' = '7.0.'; 'net6.0' = '6.0.'; 'net5.0' = '5.0.'; 'netcoreapp3.1' = '3.1.'; 'netcoreapp2.2' = '2.2.' }
$results = [Collections.Generic.List[object]]::new()
$failed = $false

Push-Location $repositoryRoot
try {
    foreach ($framework in $Frameworks) {
        $hostPath = $sdkHost
        if ($portableVersions.ContainsKey($framework)) {
            $hostPath = Join-Path $runtimeRoot ($portableVersions[$framework] + '/dotnet.exe')
            if (-not (Test-Path -LiteralPath $hostPath -PathType Leaf)) {
                throw "Missing portable runtime for ${framework}: $hostPath. Prepare the official Windows x64 runtime ZIP in this directory; this verifier does not download or install runtimes."
            }
        }

        $settingsPath = Join-Path $runPath ($framework + '.runsettings')
        $escapedHost = [Security.SecurityElement]::Escape($hostPath)
        $settings = "<RunSettings><RunConfiguration><DotNetHostPath>$escapedHost</DotNetHostPath><MaxCpuCount>1</MaxCpuCount><TestSessionTimeout>180000</TestSessionTimeout><EnvironmentVariables><DOTNET_MULTILEVEL_LOOKUP>0</DOTNET_MULTILEVEL_LOOKUP></EnvironmentVariables></RunConfiguration></RunSettings>"
        [IO.File]::WriteAllText($settingsPath, $settings, [Text.UTF8Encoding]::new($false))
        $diagnosticPath = Join-Path $runPath ($framework + '.log')
        $trxPath = Join-Path $runPath ($framework + '.trx')
        $arguments = @('test', $project, '--framework', $framework, '--settings', $settingsPath,
            '--results-directory', $runPath, '--logger', ('trx;LogFileName=' + $framework + '.trx'),
            '--diag', $diagnosticPath, '-m:1', '/p:BuildInParallel=false')
        if ($NoBuild) { $arguments += '--no-build' }
        if ($NoRestore) { $arguments += '--no-restore' }
        & $sdkHost @arguments
        $testExitCode = $LASTEXITCODE

        $total = 0
        $passed = 0
        $testFailed = 0
        $notExecuted = 0
        if (Test-Path -LiteralPath $trxPath -PathType Leaf) {
            [xml] $trx = Get-Content -LiteralPath $trxPath -Raw
            $counters = $trx.TestRun.ResultSummary.Counters
            $total = [int] $counters.total
            $passed = [int] $counters.passed
            $testFailed = [int] $counters.failed
            $notExecuted = [int] $counters.notExecuted
        }

        # 验证实际 testhost 加载的运行时，而不是仅记录 SDK 或目标框架名称。
        $runtimeLocation = $null
        foreach ($hostLog in Get-ChildItem -LiteralPath $runPath -Filter ($framework + '.host.*.log')) {
            $match = Select-String -LiteralPath $hostLog.FullName -Pattern 'Runtime location: (.+)$' | Select-Object -First 1
            if ($null -ne $match) { $runtimeLocation = $match.Matches[0].Groups[1].Value.Trim(); break }
        }
        $runtimeVerified = $null -ne $runtimeLocation -and
            (Split-Path -Leaf $runtimeLocation).StartsWith($runtimePrefixes[$framework], [StringComparison]::Ordinal)
        if ($runtimeVerified -and $portableVersions.ContainsKey($framework)) {
            $expectedLocation = Join-Path $runtimeRoot ($portableVersions[$framework] + '/shared/Microsoft.NETCore.App/' + $portableVersions[$framework])
            $runtimeVerified = [string]::Equals([IO.Path]::GetFullPath($runtimeLocation),
                [IO.Path]::GetFullPath($expectedLocation), [StringComparison]::OrdinalIgnoreCase)
        }
        $pass = $testExitCode -eq 0 -and $total -gt 0 -and $passed -eq $total -and
            $testFailed -eq 0 -and $notExecuted -eq 0 -and $runtimeVerified
        $results.Add([pscustomobject]@{
            framework = $framework; status = $(if ($pass) { 'PASS' } else { 'FAIL' })
            exitCode = $testExitCode; total = $total; passed = $passed; failed = $testFailed
            skipped = $notExecuted; host = $hostPath; runtimeLocation = $runtimeLocation
            runtimeVerified = $runtimeVerified; trx = $trxPath; settings = $settingsPath; diagnostic = $diagnosticPath
        })
        if (-not $pass) { $failed = $true }
    }
}
finally {
    Pop-Location
    $report = [pscustomobject]@{
        status = $(if (-not $failed -and $results.Count -eq $Frameworks.Count) { 'PASS' } else { 'FAIL' })
        timeoutMilliseconds = 180000; requestedFrameworks = $Frameworks; results = $results.ToArray()
    }
    $reportPath = Join-Path $runPath 'runtime-verification.json'
    [IO.File]::WriteAllText($reportPath, ($report | ConvertTo-Json -Depth 6), [Text.UTF8Encoding]::new($false))
    Write-Output "Runtime verification report: $reportPath"
}
if ($failed) { exit 1 }
