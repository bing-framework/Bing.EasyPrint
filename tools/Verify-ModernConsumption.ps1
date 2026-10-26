[CmdletBinding()]
param(
    [ValidateSet('net10.0')]
    [string] $TargetFramework = 'net10.0',
    [string] $ResultsDirectory = 'artifacts/modern-consumer-verification',
    [string] $PackageVersion = '1.0.0',
    [string] $DotNetPath,
    [switch] $NoRestore
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$coreProject = Join-Path $repositoryRoot 'src/Bing.EasyPrint/Bing.EasyPrint.csproj'
$templatesProject = Join-Path $repositoryRoot 'src/Bing.EasyPrint.Templates/Bing.EasyPrint.Templates.csproj'
$consumerScript = Join-Path $PSScriptRoot 'Verify-PackageConsumer.ps1'
foreach ($requiredPath in @($coreProject, $templatesProject, $consumerScript)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required modern consumption input was not found: $requiredPath"
    }
}

function Resolve-RepositoryPath([string] $path) {
    if ([IO.Path]::IsPathRooted($path)) { return [IO.Path]::GetFullPath($path) }
    return [IO.Path]::GetFullPath((Join-Path $repositoryRoot $path))
}

$runId = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss') + '-' +
    [Guid]::NewGuid().ToString('N').Substring(0, 8)
$evidenceRoot = Join-Path (Resolve-RepositoryPath $ResultsDirectory) $runId
$workingRoot = Join-Path ([IO.Path]::GetTempPath()) ('EasyPrintModern-' + $runId)
$candidatePath = Join-Path $workingRoot 'packages'
$xmlPath = Join-Path $workingRoot 'xml'
$consumerPath = Join-Path $workingRoot 'consumer'
$targetsPath = Join-Path $workingRoot 'Documentation.targets'
foreach ($path in @($evidenceRoot, $workingRoot, $candidatePath, $xmlPath, $consumerPath)) {
    New-Item -ItemType Directory -Path $path -Force | Out-Null
}

$targets = @'
<Project>
  <PropertyGroup Condition="'$(MSBuildProjectName)' == 'Bing.EasyPrint' Or '$(MSBuildProjectName)' == 'Bing.EasyPrint.Templates'">
    <DocumentationFile>$(MSBuildThisFileDirectory)xml/$(TargetFramework)/$(MSBuildProjectName).xml</DocumentationFile>
  </PropertyGroup>
</Project>
'@
[IO.File]::WriteAllText($targetsPath, $targets, [Text.UTF8Encoding]::new($false))

$buildDotNet = if ([string]::IsNullOrWhiteSpace($DotNetPath)) {
    (Get-Command dotnet -ErrorAction Stop).Source
}
elseif (Test-Path -LiteralPath $DotNetPath -PathType Container) {
    $dotnetExecutable = if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) { 'dotnet.exe' } else { 'dotnet' }
    Join-Path ([IO.Path]::GetFullPath($DotNetPath)) $dotnetExecutable
}
else {
    [IO.Path]::GetFullPath($DotNetPath)
}

$packResult = [Collections.Generic.List[object]]::new()
$previousTargets = $env:DirectoryBuildTargetsPath
Push-Location $repositoryRoot
try {
    $env:DirectoryBuildTargetsPath = $targetsPath
    foreach ($project in @(
        [pscustomobject]@{ Name = 'Core'; Path = $coreProject },
        [pscustomobject]@{ Name = 'Templates'; Path = $templatesProject })) {
        $arguments = @('pack', $project.Path, '--configuration', 'Release', '--output', $candidatePath, '-m:1')
        & $buildDotNet @arguments
        $exitCode = $LASTEXITCODE
        $packResult.Add([pscustomobject]@{
            project = $project.Name
            exitCode = $exitCode
            status = if ($exitCode -eq 0) { 'PASS' } else { 'FAIL' }
        })
        if ($exitCode -ne 0) { throw "Modern package pack failed for $($project.Name)." }
    }
}
finally {
    $env:DirectoryBuildTargetsPath = $previousTargets
    Pop-Location
}

$consumerArguments = @{
    CandidatePackagesDirectory = $candidatePath
    ResultsDirectory = $consumerPath
    TargetFramework = $TargetFramework
    PackageVersion = $PackageVersion
    DotNetPath = $buildDotNet
}
if ($NoRestore) { $consumerArguments['NoRestore'] = $true }
& $consumerScript @consumerArguments
$consumerExitCode = $LASTEXITCODE
if ($consumerExitCode -ne 0) { throw "Modern package consumer failed with exit code $consumerExitCode." }

$consumerEvidence = Get-ChildItem -LiteralPath $consumerPath -Recurse -Filter '*verification.json' -File
foreach ($item in $consumerEvidence) {
    $name = if ($item.Name -eq 'consumer-verification.json') { 'consumer-verification.json' } else { $item.Name }
    Copy-Item -LiteralPath $item.FullName -Destination (Join-Path $evidenceRoot $name) -Force
}
$consumerVerificationFile = $consumerEvidence |
    Where-Object { $_.Name -eq 'consumer-verification.json' } |
    Select-Object -First 1
if ($null -eq $consumerVerificationFile) {
    throw "Consumer verification JSON was not produced under $consumerPath."
}
$consumerVerification = Get-Content -LiteralPath $consumerVerificationFile.FullName -Raw |
    ConvertFrom-Json

$evidence = [ordered]@{
    status = 'PASS'
    targetFramework = $TargetFramework
    packageVersion = $PackageVersion
    runtime = [ordered]@{
        sdkHost = $buildDotNet
        sdkVersion = (& $buildDotNet --version 2>&1 | ForEach-Object { $_.ToString() } | Select-Object -First 1)
        pwshFrameworkDescription = [System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription
        consumerFrameworkDescription = $consumerVerification.Runtime.FrameworkDescription
        consumerCoreLibLocation = $consumerVerification.Runtime.CoreLibLocation
        consumerHost = $consumerVerification.Runtime.Host
        os = [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
    }
    workingDirectory = $workingRoot
    evidenceDirectory = $evidenceRoot
    candidatePackages = $candidatePath
    consumerResults = $consumerPath
    consumerEvidence = @($consumerEvidence | ForEach-Object { $_.FullName })
    packages = $packResult.ToArray()
    isolatedDocumentationTargets = $targetsPath
}
$evidencePath = Join-Path $evidenceRoot 'modern-consumer-verification.json'
[IO.File]::WriteAllText($evidencePath, ($evidence | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
Write-Output "Modern consumption PASS ($TargetFramework): $evidenceRoot"
