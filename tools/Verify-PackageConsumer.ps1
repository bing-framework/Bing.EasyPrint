[CmdletBinding()]
param(
    [string] $CandidatePackagesDirectory = 'artifacts/t8-fixed-font-pack',
    [string] $TransitivePackagesDirectory,
    [string] $ResultsDirectory = 'artifacts/t8-package-consumer-results',
    [string] $TargetFramework = 'net7.0',
    [string] $PackageVersion = '1.0.0',
    [ValidateSet('', 'netstandard2.1')]
    [string] $PackageAssetFramework = '',
    [string] $FontPath = 'tests/Fixtures/Fonts/DejaVuSans.ttf',
    [string] $DotNetPath,
    [string] $PortableDotNet,
    [switch] $NoRestore,
    [switch] $BuildOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'tests/PackageConsumer/PackageConsumer.csproj'
$frameworkAliases = @{
    'net7' = 'net7.0'
    'net6' = 'net6.0'
    'net5' = 'net5.0'
    'netcore31' = 'netcoreapp3.1'
    'netcore22' = 'netcoreapp2.2'
}
if ($frameworkAliases.ContainsKey($TargetFramework)) {
    $TargetFramework = $frameworkAliases[$TargetFramework]
}
$supportedFrameworks = @('net7.0', 'net6.0', 'net5.0', 'netcoreapp3.1', 'netcoreapp2.2')
if ($supportedFrameworks -notcontains $TargetFramework) {
    throw "Unsupported TargetFramework '$TargetFramework'. Supported values: $($supportedFrameworks -join ', ')"
}
if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "Package consumer project was not found: $projectPath"
}

function Resolve-FullPath([string] $path, [string] $basePath) {
    if ([string]::IsNullOrWhiteSpace($path)) {
        throw 'A path argument cannot be empty.'
    }
    if ([IO.Path]::IsPathRooted($path)) {
        return [IO.Path]::GetFullPath($path)
    }
    return [IO.Path]::GetFullPath((Join-Path $basePath $path))
}

function Resolve-DotNet([string] $path) {
    if ([string]::IsNullOrWhiteSpace($path)) {
        return (Get-Command dotnet -ErrorAction Stop).Source
    }
    $candidate = [IO.Path]::GetFullPath($path)
    if (Test-Path -LiteralPath $candidate -PathType Container) {
        $candidate = Join-Path $candidate 'dotnet.exe'
    }
    if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
        throw "dotnet executable was not found: $candidate"
    }
    return $candidate
}

function Escape-Xml([string] $value) {
    return [System.Security.SecurityElement]::Escape($value)
}

function Assert-CandidatePackageResolved([string] $packageId) {
    $packageRoot = Join-Path $restorePackagesPath (Join-Path $packageId.ToLowerInvariant() $PackageVersion)
    $resolvedPackage = Get-ChildItem -LiteralPath $packageRoot -Filter '*.nupkg' -File -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $resolvedPackage) {
        throw "Restored package archive was not found for $packageId ${PackageVersion}: $packageRoot"
    }

    $candidatePackage = Join-Path $candidatePath "$packageId.$PackageVersion.nupkg"
    $candidateHash = (Get-FileHash -LiteralPath $candidatePackage -Algorithm SHA256).Hash
    $resolvedHash = (Get-FileHash -LiteralPath $resolvedPackage.FullName -Algorithm SHA256).Hash
    if ($candidateHash -ne $resolvedHash) {
        throw "Resolved $packageId $PackageVersion does not match the candidate package archive."
    }
}

function Get-SelectedPackageEvidence([object] $assets, [string] $packageId) {
    $targetProperty = $assets.targets.PSObject.Properties |
        Where-Object { $_.Name -eq $TargetFramework } |
        Select-Object -First 1
    if ($null -eq $targetProperty) {
        $targetProperty = $assets.targets.PSObject.Properties |
            Where-Object { $_.Name -like "$TargetFramework/*" } |
            Select-Object -First 1
    }
    if ($null -eq $targetProperty) {
        throw "project.assets.json has no target for $TargetFramework."
    }

    $packageProperty = $targetProperty.Value.PSObject.Properties |
        Where-Object { $_.Name -eq "$packageId/$PackageVersion" } |
        Select-Object -First 1
    if ($null -eq $packageProperty) {
        throw "project.assets.json has no resolved entry for $packageId/$PackageVersion."
    }

    $assetPaths = [System.Collections.Generic.List[string]]::new()
    foreach ($sectionName in @('compile', 'runtime')) {
        $sectionProperty = $packageProperty.Value.PSObject.Properties[$sectionName]
        if ($null -eq $sectionProperty) {
            continue
        }
        foreach ($assetProperty in $sectionProperty.Value.PSObject.Properties) {
            [void]$assetPaths.Add([string]$assetProperty.Name)
        }
    }

    $uniquePaths = @($assetPaths | Sort-Object -Unique)
    if (-not [string]::IsNullOrEmpty($PackageAssetFramework)) {
        $assetRelativePath = "lib/$PackageAssetFramework/$packageId.dll"
        $restoredDll = Join-Path $restorePackagesPath ($packageId.ToLowerInvariant() + '/' + $PackageVersion + '/' + $assetRelativePath)
        $executedDll = Join-Path $outputPath ($packageId + '.dll')
        if (-not (Test-Path -LiteralPath $restoredDll -PathType Leaf)) {
            throw "Candidate package does not contain the explicit asset: $restoredDll"
        }
        if ((Get-FileHash -LiteralPath $restoredDll).Hash -ne (Get-FileHash -LiteralPath $executedDll).Hash) {
            throw "Consumer output does not match the explicit candidate asset: $executedDll"
        }
        return [ordered]@{
            PackageId = $packageId; Version = $PackageVersion; AssetsTargetFramework = $targetProperty.Name
            Selection = 'ExplicitCandidateAsset'; LibTfms = @($PackageAssetFramework)
            AssetPaths = @($assetRelativePath); OutputMatched = $true
        }
    }
    $libTfms = @($uniquePaths |
        ForEach-Object {
            if ($_ -match '^lib/([^/]+)/') {
                $Matches[1]
            }
        } |
        Sort-Object -Unique)
    if ($libTfms.Count -eq 0) {
        throw "project.assets.json has no lib TFM for $packageId/$PackageVersion."
    }

    return [ordered]@{
        PackageId = $packageId
        Version = $PackageVersion
        AssetsTargetFramework = $targetProperty.Name
        LibTfms = $libTfms
        AssetPaths = $uniquePaths
    }
}

function Get-TaggedOutputValue([string[]] $output, [string] $tag) {
    $line = $output |
        Where-Object { $_ -like "$tag=*" } |
        Select-Object -First 1
    if ($null -eq $line) {
        return $null
    }
    return $line.Substring($tag.Length + 1)
}

$candidatePath = Resolve-FullPath $CandidatePackagesDirectory $repositoryRoot
$fontPathFull = Resolve-FullPath $FontPath $repositoryRoot
$resultsPath = Resolve-FullPath $ResultsDirectory $repositoryRoot
$transitivePath = if ([string]::IsNullOrWhiteSpace($TransitivePackagesDirectory)) {
    if ([string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES)) {
        Resolve-FullPath (Join-Path $env:USERPROFILE '.nuget/packages') $repositoryRoot
    }
    else {
        Resolve-FullPath $env:NUGET_PACKAGES $repositoryRoot
    }
}
else {
    Resolve-FullPath $TransitivePackagesDirectory $repositoryRoot
}

foreach ($requiredPath in @($candidatePath, $transitivePath, $fontPathFull)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required package/font path was not found: $requiredPath"
    }
}
foreach ($packageId in @('Bing.EasyPrint', 'Bing.EasyPrint.Templates')) {
    $packagePath = Join-Path $candidatePath "$packageId.$PackageVersion.nupkg"
    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) {
        throw "Candidate package was not found: $packagePath"
    }
}

$buildDotNet = Resolve-DotNet $DotNetPath
$runDotNet = if ([string]::IsNullOrWhiteSpace($PortableDotNet)) {
    $buildDotNet
}
else {
    Resolve-DotNet $PortableDotNet
}

$assetSuffix = if ([string]::IsNullOrEmpty($PackageAssetFramework)) { '' } else { '-' + $PackageAssetFramework }
$runRoot = Join-Path $resultsPath ("consumer-$TargetFramework$assetSuffix")
$outputPath = Join-Path $runRoot 'output'
$intermediatePath = Join-Path $runRoot 'obj'
$restorePackagesPath = Join-Path $runRoot 'packages'
$nugetConfigPath = Join-Path $runRoot 'NuGet.Config'
$assemblyPath = Join-Path $outputPath 'Bing.EasyPrint.PackageConsumer.dll'
$assetsPath = Join-Path $intermediatePath 'project.assets.json'
$evidencePath = Join-Path $runRoot 'consumer-verification.json'

function Write-VerificationEvidence(
    [string] $status,
    [string] $runStatus,
    [int] $runExitCode,
    [string] $runtimeHost,
    [string] $frameworkDescription,
    [string] $coreLibLocation,
    [string[]] $programOutput) {
    $evidence = [ordered]@{
        Status = $status
        TargetFramework = $TargetFramework
        PackageVersion = $PackageVersion
        CandidatePackagesMatched = $true
        Packages = $packageEvidence
        SelectedPackageAssets = $selectedAssetEvidence
        ProjectAssetsPath = $assetsPath
        Runtime = [ordered]@{
            Status = $runStatus
            ExitCode = $runExitCode
            Host = $runtimeHost
            FrameworkDescription = $frameworkDescription
            CoreLibLocation = $coreLibLocation
            DotNetMultilevelLookup = '0-during-run'
        }
        ProgramOutput = @($programOutput)
    }
    $json = $evidence | ConvertTo-Json -Depth 12
    [IO.File]::WriteAllText($evidencePath, $json, [Text.UTF8Encoding]::new($false))
}

New-Item -ItemType Directory -Path $runRoot -Force | Out-Null
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
New-Item -ItemType Directory -Path $intermediatePath -Force | Out-Null
New-Item -ItemType Directory -Path $restorePackagesPath -Force | Out-Null

$candidateXml = Escape-Xml $candidatePath
$transitiveXml = Escape-Xml $transitivePath
$nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="candidate" value="$candidateXml" />
    <add key="transitive" value="$transitiveXml" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="candidate">
      <package pattern="Bing.EasyPrint" />
      <package pattern="Bing.EasyPrint.Templates" />
    </packageSource>
    <packageSource key="transitive">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"@
[IO.File]::WriteAllText($nugetConfigPath, $nugetConfig, [Text.UTF8Encoding]::new($false))

$commonArguments = @(
    '-m:1'
    '/p:BuildInParallel=false'
    '/p:RestoreDisableParallel=true'
    "/p:ConsumerTargetFramework=$TargetFramework"
    "/p:EasyPrintPackageVersion=$PackageVersion"
    "/p:RestorePackagesPath=$restorePackagesPath"
    "/p:RestoreConfigFile=$nugetConfigPath"
    "/p:BaseIntermediateOutputPath=$($intermediatePath.TrimEnd('\'))\"
)
if (-not [string]::IsNullOrEmpty($PackageAssetFramework)) {
    $commonArguments += "/p:ConsumerPackageAsset=$PackageAssetFramework"
}

$buildArguments = @(
    'build'
    $projectPath
    '--configuration'
    'Release'
    '--framework'
    $TargetFramework
    '--output'
    $outputPath
    '--no-incremental'
) + $commonArguments

if (-not $NoRestore) {
    # Separate restore from build so package extraction is complete before MSBuild resolves CopyLocal assets.
    $restoreArguments = @(
        'restore'
        $projectPath
    ) + $commonArguments

    Push-Location $repositoryRoot
    try {
        & $buildDotNet @restoreArguments
        $restoreExitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
    if ($restoreExitCode -ne 0) {
        exit $restoreExitCode
    }
}
$buildArguments += '--no-restore'

Push-Location $repositoryRoot
try {
    & $buildDotNet @buildArguments
    $buildExitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}
if ($buildExitCode -ne 0) {
    exit $buildExitCode
}

if (-not (Test-Path -LiteralPath $assetsPath -PathType Leaf)) {
    throw "project.assets.json was not produced: $assetsPath"
}

if (-not [string]::IsNullOrEmpty($PackageAssetFramework)) {
    # Explicit asset references bypass NuGet's normal CopyLocal item. Restore still supplies
    # the transitive runtime assemblies, so copy any package DLLs omitted by that reference path.
    $explicitRuntimeDependencies = Get-ChildItem -LiteralPath $restorePackagesPath -Recurse -Filter 'Microsoft.Extensions.DependencyInjection.Abstractions.dll' -File |
        Select-Object -ExpandProperty FullName
    if ($explicitRuntimeDependencies.Count -eq 0) {
        throw "Explicit package asset restore did not provide Microsoft.Extensions.DependencyInjection.Abstractions.dll."
    }
    foreach ($dependencyPath in $explicitRuntimeDependencies) {
        Copy-Item -LiteralPath $dependencyPath -Destination (Join-Path $outputPath ([IO.Path]::GetFileName($dependencyPath))) -Force
    }
}

$assetsDocument = Get-Content -LiteralPath $assetsPath -Raw | ConvertFrom-Json
$packageEvidence = @()
$selectedAssetEvidence = @()
foreach ($packageId in @('Bing.EasyPrint', 'Bing.EasyPrint.Templates')) {
    Assert-CandidatePackageResolved $packageId
    $packageRoot = Join-Path $restorePackagesPath (Join-Path $packageId.ToLowerInvariant() $PackageVersion)
    $resolvedPackage = Get-ChildItem -LiteralPath $packageRoot -Filter '*.nupkg' -File |
        Select-Object -First 1
    $candidatePackage = Join-Path $candidatePath "$packageId.$PackageVersion.nupkg"
    $candidateHash = (Get-FileHash -LiteralPath $candidatePackage -Algorithm SHA256).Hash
    $resolvedHash = (Get-FileHash -LiteralPath $resolvedPackage.FullName -Algorithm SHA256).Hash
    $packageEvidence += [ordered]@{
        PackageId = $packageId
        Version = $PackageVersion
        CandidateArchive = $candidatePackage
        ResolvedArchive = $resolvedPackage.FullName
        CandidateSha256 = $candidateHash
        ResolvedSha256 = $resolvedHash
        HashMatched = $candidateHash -eq $resolvedHash
    }
    $selectedAssetEvidence += Get-SelectedPackageEvidence $assetsDocument $packageId
}

if ($BuildOnly) {
    Write-VerificationEvidence 'PASS' 'SKIPPED_BUILD_ONLY' -1 $null $null $null @()
    Write-Output "Package consumer build PASS ($TargetFramework): $outputPath"
    exit 0
}
if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
    throw "Consumer assembly was not produced: $assemblyPath"
}

$runOutput = @()
$runExitCode = -1
$previousMultiLevelLookup = $env:DOTNET_MULTILEVEL_LOOKUP
Push-Location $repositoryRoot
try {
    $env:DOTNET_MULTILEVEL_LOOKUP = '0'
    $runOutput = @(& $runDotNet $assemblyPath '--font-path' $fontPathFull '--runtime-host' $runDotNet 2>&1 |
        ForEach-Object { $_.ToString() })
    $runExitCode = $LASTEXITCODE
}
finally {
    if ($null -eq $previousMultiLevelLookup) {
        Remove-Item Env:DOTNET_MULTILEVEL_LOOKUP -ErrorAction SilentlyContinue
    }
    else {
        $env:DOTNET_MULTILEVEL_LOOKUP = $previousMultiLevelLookup
    }
    Pop-Location
}
$runOutput | ForEach-Object { Write-Output $_ }
$runtimeHost = Get-TaggedOutputValue $runOutput 'RuntimeHost'
$frameworkDescription = Get-TaggedOutputValue $runOutput 'FrameworkDescription'
$coreLibLocation = Get-TaggedOutputValue $runOutput 'CoreLibLocation'
$runStatus = if ($runExitCode -eq 0) { 'PASS' } else { 'FAIL' }
Write-VerificationEvidence $runStatus $runStatus $runExitCode $runtimeHost $frameworkDescription $coreLibLocation $runOutput
if ($runExitCode -ne 0) {
    exit $runExitCode
}
Write-Output "Package consumer PASS ($TargetFramework): $runRoot"
