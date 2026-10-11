[CmdletBinding()]
param(
    [string] $OutputDirectory = 'artifacts/designer-host-verification-bin',
    [string] $ResultsDirectory = 'artifacts/designer-host-verification-tests',
    [switch] $NoRestore
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'tests/DesignerHost.Tests/DesignerHost.Tests.csproj'

if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
    throw "DesignerHost.Tests project was not found: $projectPath"
}

$dotnet = Get-Command dotnet -ErrorAction Stop
$outputPath = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
    [IO.Path]::GetFullPath($OutputDirectory)
}
else {
    [IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$resultsPath = if ([IO.Path]::IsPathRooted($ResultsDirectory)) {
    [IO.Path]::GetFullPath($ResultsDirectory)
}
else {
    [IO.Path]::GetFullPath((Join-Path $repositoryRoot $ResultsDirectory))
}

$arguments = @(
    'test'
    $projectPath
    '--framework'
    'net8.0'
    '--output'
    $outputPath
    '--results-directory'
    $resultsPath
    '--logger'
    'trx;LogFileName=designer-host-net8.trx'
    '-m:1'
    '/p:BuildInParallel=false'
)
if ($NoRestore) {
    $arguments += '--no-restore'
}

Push-Location $repositoryRoot
try {
    & $dotnet.Source @arguments
    $exitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}

if ($exitCode -ne 0) {
    exit $exitCode
}
