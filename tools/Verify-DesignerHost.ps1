[CmdletBinding()]
param(
    [string] $OutputDirectory = 'artifacts/designer-host-verification-bin',
    [string] $ResultsDirectory = 'artifacts/designer-host-verification-tests',
    [string] $DiagnosticPath,
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
$diagnosticPath = if ([string]::IsNullOrWhiteSpace($DiagnosticPath)) {
    Join-Path $resultsPath 'designer-host-net10.diag.log'
}
elseif ([IO.Path]::IsPathRooted($DiagnosticPath)) {
    [IO.Path]::GetFullPath($DiagnosticPath)
}
else {
    [IO.Path]::GetFullPath((Join-Path $repositoryRoot $DiagnosticPath))
}
New-Item -ItemType Directory -Path $resultsPath -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path -Parent $diagnosticPath) -Force | Out-Null

$arguments = @(
    'test'
    $projectPath
    '--framework'
    'net10.0'
    '--output'
    $outputPath
    '--results-directory'
    $resultsPath
    '--logger'
    'trx;LogFileName=designer-host-net10.trx'
    '--diag'
    $diagnosticPath
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
