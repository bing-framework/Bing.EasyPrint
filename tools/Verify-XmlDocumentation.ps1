[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$frameworks = @('net7.0', 'net6.0', 'netcoreapp3.1', 'netstandard2.0', 'netstandard2.1')
$projects = @(
    [pscustomobject]@{
        Name = 'Core'
        Path = Join-Path $repositoryRoot 'src/Bing.EasyPrint/Bing.EasyPrint.csproj'
        Assembly = 'Bing.EasyPrint'
    },
    [pscustomobject]@{
        Name = 'Templates'
        Path = Join-Path $repositoryRoot 'src/Bing.EasyPrint.Templates/Bing.EasyPrint.Templates.csproj'
        Assembly = 'Bing.EasyPrint.Templates'
    }
)
$xmlWarningCodes = @(
    'CS0419',
    'CS1570',
    'CS1571',
    'CS1572',
    'CS1573',
    'CS1574',
    'CS1584',
    'CS1587',
    'CS1591',
    'CS1658',
    'CS1710',
    'CS1711',
    'CS1712',
    'CS1723'
) -join ';'
$encodedXmlWarningCodes = $xmlWarningCodes.Replace(';', '%3B')

$runId = '{0}-{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss-fff'), ([guid]::NewGuid().ToString('N').Substring(0, 8))
$startedAt = (Get-Date).ToString('o')
$runDirectory = Join-Path $repositoryRoot (Join-Path 'artifacts/xml-documentation-verification' $runId)
$null = New-Item -ItemType Directory -Path $runDirectory -Force

function Invoke-DocumentationBuild {
    param(
        [Parameter(Mandatory = $true)] [pscustomobject] $Project,
        [Parameter(Mandatory = $true)] [string] $Framework,
        [Parameter(Mandatory = $true)] [bool] $BuildProjectReferences
    )

    $projectDirectory = Join-Path $runDirectory (Join-Path $Project.Name $Framework)
    $null = New-Item -ItemType Directory -Path $projectDirectory -Force
    $xmlPath = Join-Path $projectDirectory ($Project.Assembly + '.xml')
    $logPath = Join-Path $projectDirectory 'build.log'
    $arguments = @(
        'build',
        $Project.Path,
        '-t:Rebuild',
        '--configuration', 'Release',
        '--framework', $Framework,
        '--verbosity', 'minimal',
        '-p:GenerateDocumentationFile=true',
        '-p:NoWarn=',
        "-p:WarningsAsErrors=$encodedXmlWarningCodes",
        "-p:DocumentationFile=$xmlPath",
        "-p:BuildProjectReferences=$($BuildProjectReferences.ToString().ToLowerInvariant())"
    )

    Write-Host "[XML] $($Project.Name) $Framework"
    Write-Host ('dotnet ' + ($arguments -join ' '))
    $output = & dotnet @arguments 2>&1
    $exitCode = $LASTEXITCODE
    $output | Tee-Object -FilePath $logPath | Out-Host

    if ($exitCode -eq 0 -and -not (Test-Path -LiteralPath $xmlPath -PathType Leaf)) {
        $exitCode = 1
        Add-Content -LiteralPath $logPath -Value 'ERROR: build succeeded but the isolated XML documentation file was not created.'
        Write-Error 'Build succeeded but the isolated XML documentation file was not created.' -ErrorAction Continue
    }

    return [pscustomobject]@{
        Project = $Project.Name
        Framework = $Framework
        Status = if ($exitCode -eq 0) { 'PASS' } else { 'FAIL' }
        ExitCode = $exitCode
        Xml = $xmlPath
        Log = $logPath
        BuildProjectReferences = $BuildProjectReferences
        Command = @('dotnet') + $arguments
    }
}

$results = [System.Collections.Generic.List[object]]::new()
foreach ($framework in $frameworks) {
    $coreResult = Invoke-DocumentationBuild -Project $projects[0] -Framework $framework -BuildProjectReferences $true
    $results.Add($coreResult)

    if ($coreResult.Status -eq 'PASS') {
        $templateResult = Invoke-DocumentationBuild -Project $projects[1] -Framework $framework -BuildProjectReferences $false
        $results.Add($templateResult)
    }
    else {
        $results.Add([pscustomobject]@{
            Project = 'Templates'
            Framework = $framework
            Status = 'SKIP'
            ExitCode = $null
            Xml = $null
            Log = $null
            BuildProjectReferences = $false
            Command = $null
            Reason = 'Core target failed; the dependent project was not built against a stale assembly.'
        })
    }
}

$failed = @($results | Where-Object { $_.Status -ne 'PASS' }).Count
$summary = [pscustomobject]@{
    RunId = $runId
    StartedAt = $startedAt
    Configuration = 'Release'
    Frameworks = $frameworks
    Projects = @('Core', 'Templates')
    XmlWarningCodesAsErrors = $xmlWarningCodes -split ';'
    Result = if ($failed -eq 0) { 'PASS' } else { 'FAIL' }
    PassedBuilds = @($results | Where-Object Status -eq 'PASS').Count
    FailedOrSkippedBuilds = $failed
    Results = @($results)
}
$manifestPath = Join-Path $runDirectory 'verification.json'
$json = ConvertTo-Json -InputObject $summary -Depth 8
[System.IO.File]::WriteAllText($manifestPath, $json, [System.Text.UTF8Encoding]::new($false))

Write-Host "Result: $($summary.Result); passed=$($summary.PassedBuilds); failed-or-skipped=$($summary.FailedOrSkippedBuilds)"
Write-Host "Manifest: $manifestPath"
if ($failed -gt 0) {
    exit 1
}
