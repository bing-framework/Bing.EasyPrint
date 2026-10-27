[CmdletBinding()]
param(
    [string] $ResultsDirectory = 'artifacts/optimization-expansion-20261025/post-review-20261026/batch-consumer',
    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'samples/BatchConsumer/BatchConsumer.csproj'
$templatePath = Join-Path $repositoryRoot 'samples/Phase5/export-template.json'
$dotnetPath = (Get-Command dotnet -ErrorAction Stop).Source
$resultsPath = if ([IO.Path]::IsPathRooted($ResultsDirectory)) {
    [IO.Path]::GetFullPath($ResultsDirectory)
}
else {
    [IO.Path]::GetFullPath((Join-Path $repositoryRoot $ResultsDirectory))
}
$runName = 'run-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$runPath = Join-Path $resultsPath $runName
$utf8 = [Text.UTF8Encoding]::new($false)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
New-Item -ItemType Directory -Path $runPath -Force | Out-Null

$buildOutput = @()
if (-not $NoBuild) {
    Push-Location $repositoryRoot
    try {
        $buildOutput = @(& $dotnetPath build $projectPath --configuration Release 2>&1 |
            ForEach-Object { $_.ToString() })
        $buildExitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
    [IO.File]::WriteAllLines((Join-Path $runPath 'build.log'), $buildOutput, $utf8)
    if ($buildExitCode -ne 0) {
        throw "BatchConsumer build failed with exit code $buildExitCode. Evidence: $runPath"
    }
}

$assemblyPath = Join-Path $repositoryRoot 'samples/BatchConsumer/bin/Release/net6.0/BatchConsumer.dll'
if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
    throw "BatchConsumer assembly was not found: $assemblyPath"
}
if (-not (Test-Path -LiteralPath $templatePath -PathType Leaf)) {
    throw "Required barcode template was not found: $templatePath"
}
$template = Get-Content -LiteralPath $templatePath -Raw | ConvertFrom-Json
$codeElements = @(
    foreach ($row in $template.Rows) {
        foreach ($cell in $row.Cells) {
            foreach ($element in $cell.Elements) {
                if ([int] $element.Type -in @(3, 4, 10, 11)) { $element }
            }
        }
    }
)
if ($codeElements.Count -eq 0) {
    throw 'Regression template must contain a software-encodable barcode element.'
}

$mockHost = {
    param(
        [int] $Port,
        [string] $Scenario,
        [string] $ReadyPath,
        [string] $RequestLogPath,
        [string] $ServerErrorPath,
        [int] $MaxRequests
    )

    $ErrorActionPreference = 'Stop'
    Set-StrictMode -Version Latest
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $utf8 = [Text.UTF8Encoding]::new($false)
    $listener = [Net.HttpListener]::new()
    $listener.Prefixes.Add("http://127.0.0.1:$Port/")
    $requestLog = [Collections.Generic.List[object]]::new()

    function Add-ZipTextEntry($Archive, [string] $Name, [string] $Text) {
        $entry = $Archive.CreateEntry($Name)
        $stream = $entry.Open()
        try {
            $bytes = $utf8.GetBytes($Text)
            $stream.Write($bytes, 0, $bytes.Length)
        }
        finally {
            $stream.Dispose()
        }
    }

    function Add-ZipBytesEntry($Archive, [string] $Name, [byte[]] $Bytes) {
        $entry = $Archive.CreateEntry($Name)
        $stream = $entry.Open()
        try {
            if ($Bytes.Length -gt 0) { $stream.Write($Bytes, 0, $Bytes.Length) }
        }
        finally {
            $stream.Dispose()
        }
    }

    function Write-Response($Context, [int] $StatusCode, [byte[]] $Bytes, [string] $ContentType) {
        $Context.Response.StatusCode = $StatusCode
        $Context.Response.ContentType = $ContentType
        $Context.Response.ContentLength64 = $Bytes.Length
        if ($Bytes.Length -gt 0) { $Context.Response.OutputStream.Write($Bytes, 0, $Bytes.Length) }
        $Context.Response.Close()
    }

    try {
        $listener.Start()
        [IO.File]::WriteAllText($ReadyPath, 'ready', $utf8)
        for ($requestNumber = 0; $requestNumber -lt $MaxRequests; $requestNumber++) {
            $context = $listener.GetContext()
            $reader = [IO.StreamReader]::new($context.Request.InputStream, $context.Request.ContentEncoding)
            try { $requestText = $reader.ReadToEnd() } finally { $reader.Dispose() }
            $payload = $requestText | ConvertFrom-Json
            $requiredValues = $context.Request.Headers.GetValues('X-EasyPrint-Required-Features')
            $requestEntry = [ordered]@{
                path = $context.Request.Url.AbsolutePath
                requiredFeatureHeader = $context.Request.Headers['X-EasyPrint-Required-Features']
                requiredFeatureHeaderValues = @($requiredValues)
                recordOffset = [int] $payload.recordOffset
                totalRecords = [int] $payload.totalRecords
                recordIndexes = @($payload.records | ForEach-Object { [int] $_.index })
                exportFormats = @($payload.exportFormats | ForEach-Object { [string] $_ })
                templateCodeElementCount = @(
                    foreach ($row in $payload.template.Rows) {
                        foreach ($cell in $row.Cells) {
                            foreach ($element in $cell.Elements) {
                                if ([int] $element.Type -in @(3, 4, 10, 11)) { $element }
                            }
                        }
                    }
                ).Count
            }
            $requestLog.Add($requestEntry)
            [IO.File]::AppendAllText($RequestLogPath,
                (ConvertTo-Json -InputObject $requestEntry -Depth 30 -Compress) + [Environment]::NewLine, $utf8)

            if ($Scenario -eq 'old-reject') {
                $errorBytes = $utf8.GetBytes('{"code":"ContractFeatureUnavailable"}')
                Write-Response $context 400 $errorBytes 'application/json'
                continue
            }
            if ($Scenario -eq 'ignore-header') {
                $context.Response.Headers['X-EasyPrint-Contract-Version'] = '1'
                $context.Response.Headers['X-EasyPrint-Features'] = 'batch-export'
                $emptyZip = [byte[]] @(0x50, 0x4b, 0x05, 0x06, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
                Write-Response $context 200 $emptyZip 'application/zip'
                continue
            }

            $recordResults = [Collections.Generic.List[object]]::new()
            $errors = [Collections.Generic.List[object]]::new()
            $zipFiles = [Collections.Generic.List[object]]::new()
            $rawByteLength = 0L
            $succeeded = 0
            $failed = 0
            foreach ($inputRecord in @($payload.records)) {
                $index = [int] $inputRecord.index
                $recordStatus = 'completed'
                if ($Scenario -eq 'all-failed' -or ($Scenario -eq 'partial' -and $index -eq 1)) {
                    $recordStatus = 'failed'
                }
                $recordFiles = [Collections.Generic.List[object]]::new()
                if ($recordStatus -eq 'completed') {
                    $pdfPath = ('records/{0:000000}/exports/pdf/record.pdf' -f $index)
                    $svgPath = ('records/{0:000000}/exports/svg/copy-0001/page-0001-element-0001.svg' -f $index)
                    if ($Scenario -ne 'missing-pdf') {
                        if ($Scenario -eq 'empty-export') {
                            $pdfBytes = [Array]::CreateInstance([byte], 0)
                        }
                        else {
                            $pdfBytes = [Text.Encoding]::ASCII.GetBytes('%PDF-mock')
                        }
                        $recordFiles.Add([ordered]@{ file = $pdfPath; format = 'pdf'; pageCount = 1; copies = 1 })
                        if ($Scenario -ne 'missing-entry') {
                            $zipFiles.Add([pscustomobject]@{ path = $pdfPath; bytes = $pdfBytes })
                            $rawByteLength += $pdfBytes.Length
                        }
                    }
                    if ($Scenario -ne 'missing-svg') {
                        $svgBytes = [Text.Encoding]::UTF8.GetBytes('<svg><path d="M0 0"/></svg>')
                        $recordFiles.Add([ordered]@{
                            file = $svgPath; format = 'svg'; pageIndex = 0; sourcePageIndex = 0
                            elementIndex = 0; copyIndex = 0; kind = 'Barcode'; placement = 'Page'
                        })
                        $zipFiles.Add([pscustomobject]@{ path = $svgPath; bytes = $svgBytes })
                        $rawByteLength += $svgBytes.Length
                    }
                    $succeeded++
                }
                else {
                    $failed++
                    $errors.Add([ordered]@{
                        recordIndex = $index; code = 'BatchRecordFailed'; phase = 'Render'
                        fieldPath = 'data.Value'; sourcePath = 'Rows[0].Cells[0].Elements[0]'
                        message = 'Mock record failure'
                    })
                }
                $reportedStatus = if ($Scenario -eq 'unknown-status') { 'queued' } else { $recordStatus }
                $recordResult = [ordered]@{ index = $index; status = $reportedStatus; files = $recordFiles.ToArray() }
                if ($Scenario -eq 'missing-status') { $null = $recordResult.Remove('status') }
                $recordResults.Add($recordResult)
            }

            $status = if ($failed -eq 0) { 'completed' } elseif ($succeeded -eq 0) { 'all_failed' } else { 'partial' }
            $remainingPageBudget = [int] $payload.remainingPages - $succeeded
            $remainingByteBudget = [long] $payload.remainingBytes - $rawByteLength
            $manifest = [ordered]@{
                status = $status; recordOffset = [int] $payload.recordOffset; totalRecords = [int] $payload.totalRecords
                succeeded = $succeeded; failed = $failed; physicalPages = $succeeded; byteLength = $rawByteLength
                remainingPages = $remainingPageBudget; remainingBytes = $remainingByteBudget
                records = $recordResults.ToArray(); files = @(); errors = $errors.ToArray()
            }
            $zipStream = [IO.MemoryStream]::new()
            $archive = [IO.Compression.ZipArchive]::new($zipStream, [IO.Compression.ZipArchiveMode]::Create, $true)
            try {
                foreach ($file in $zipFiles) { Add-ZipBytesEntry $archive $file.path $file.bytes }
                Add-ZipTextEntry $archive 'manifest.json' (ConvertTo-Json -InputObject $manifest -Depth 50 -Compress)
                $csvText = "recordIndex,code,phase,fieldPath,sourcePath,message`r`n"
                foreach ($errorRow in $errors) {
                    $csvText += ('{0},"{1}","{2}","{3}","{4}","{5}"`r`n' -f
                        $errorRow.recordIndex, $errorRow.code, $errorRow.phase, $errorRow.fieldPath,
                        $errorRow.sourcePath, $errorRow.message)
                }
                Add-ZipTextEntry $archive 'errors.csv' $csvText
            }
            finally {
                $archive.Dispose()
            }
            $responseBytes = $zipStream.ToArray()
            $zipStream.Dispose()
            $context.Response.Headers['X-EasyPrint-Contract-Version'] = if ($Scenario -eq 'wrong-version') { '2' } else { '1' }
            $context.Response.Headers['X-EasyPrint-Features'] = 'batch-export,page-pdf-export,barcode-svg-export'
            $context.Response.Headers['X-EasyPrint-Batch-Remaining-Pages'] = $remainingPageBudget.ToString(
                [Globalization.CultureInfo]::InvariantCulture)
            $context.Response.Headers['X-EasyPrint-Batch-Remaining-Bytes'] = $remainingByteBudget.ToString(
                [Globalization.CultureInfo]::InvariantCulture)
            Write-Response $context 200 $responseBytes 'application/zip'
        }
    }
    catch {
        [IO.File]::WriteAllText($ServerErrorPath, ($_ | Out-String), $utf8)
        throw
    }
    finally {
        $listener.Stop()
        $listener.Close()
    }
}

function Get-FreeLoopbackPort {
    $tcpListener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $tcpListener.Start()
    try { return ([Net.IPEndPoint] $tcpListener.LocalEndpoint).Port }
    finally { $tcpListener.Stop() }
}

function Read-ArchiveManifest([string] $Path) {
    $archive = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entry = $archive.GetEntry('manifest.json')
        if ($null -eq $entry) { throw "Output ZIP has no manifest.json: $Path" }
        $reader = [IO.StreamReader]::new($entry.Open(), [Text.Encoding]::UTF8)
        try { return ($reader.ReadToEnd() | ConvertFrom-Json) }
        finally { $reader.Dispose() }
    }
    finally { $archive.Dispose() }
}

function Assert-RequestLog([string] $Path, [int] $RecordCount, [string] $CaseName) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "$CaseName produced no request log." }
    $requests = [Collections.Generic.List[object]]::new()
    foreach ($line in Get-Content -LiteralPath $Path) {
        if (-not [string]::IsNullOrWhiteSpace($line)) {
            $requests.Add(($line | ConvertFrom-Json))
        }
    }
    $expectedHeader = 'batch-export,page-pdf-export,barcode-svg-export'
    $expectedRequestCount = [Math]::Ceiling($RecordCount / 25.0)
    if ($CaseName -eq 'old-host-reject') { $expectedRequestCount = 1 }
    if ($requests.Count -ne $expectedRequestCount) {
        throw "$CaseName sent $($requests.Count) requests; expected $expectedRequestCount."
    }
    foreach ($request in $requests) {
        if ($request.requiredFeatureHeader -ne $expectedHeader) {
            throw "$CaseName sent an unexpected Required-Features header: $($request.requiredFeatureHeader)"
        }
        if (@($request.requiredFeatureHeaderValues).Count -ne 1) {
            throw "$CaseName sent the Required-Features header more than once."
        }
        if (($request.exportFormats -join ',') -ne 'pdf,svg') {
            throw "$CaseName did not request both PDF and SVG."
        }
        if ($request.templateCodeElementCount -lt 1) {
            throw "$CaseName sent a template without a software-encodable barcode."
        }
    }
    if ($RecordCount -eq 26) {
        if ($requests[0].recordOffset -ne 0 -or @($requests[0].recordIndexes).Count -ne 25 -or
            $requests[1].recordOffset -ne 25 -or @($requests[1].recordIndexes).Count -ne 1) {
            throw 'The 26-record case did not cross the 25-record boundary as expected.'
        }
    }
    return $requests.ToArray()
}

function Invoke-ConsumerCase(
    [string] $Name,
    [string] $Scenario,
    [int] $RecordCount,
    [int] $ExpectedExitCode,
    [string] $ExpectedStatus,
    [string] $ExpectedError,
    [bool] $PreserveExistingOutput,
    [bool] $ExpectNoOutput) {

    $casePath = Join-Path $runPath $Name
    New-Item -ItemType Directory -Path $casePath -Force | Out-Null
    $recordsPath = Join-Path $casePath 'records.json'
    $outputPath = Join-Path $casePath 'result.zip'
    $requestLogPath = Join-Path $casePath 'requests.ndjson'
    $serverReadyPath = Join-Path $casePath 'host-ready.txt'
    $serverErrorPath = Join-Path $casePath 'host-error.txt'
    $stdoutPath = Join-Path $casePath 'stdout.txt'
    $stderrPath = Join-Path $casePath 'stderr.txt'
    $records = @(
        for ($index = 0; $index -lt $RecordCount; $index++) {
            [ordered]@{ Value = ('BATCH-{0:000}' -f ($index + 1)) }
        }
    )
    [IO.File]::WriteAllText($recordsPath, (ConvertTo-Json -InputObject $records -Depth 10), $utf8)

    $sentinel = [Text.Encoding]::UTF8.GetBytes('existing-result-preserved')
    if ($PreserveExistingOutput) { [IO.File]::WriteAllBytes($outputPath, $sentinel) }

    $port = Get-FreeLoopbackPort
    $maxRequests = [int] [Math]::Ceiling($RecordCount / 25.0)
    $job = Start-Job -ScriptBlock $mockHost -ArgumentList @(
        $port, $Scenario, $serverReadyPath, $requestLogPath, $serverErrorPath, $maxRequests)
    try {
        $readyDeadline = [DateTime]::UtcNow.AddSeconds(15)
        while (-not (Test-Path -LiteralPath $serverReadyPath -PathType Leaf) -and
            $job.State -notin @('Failed', 'Completed') -and [DateTime]::UtcNow -lt $readyDeadline) {
            Start-Sleep -Milliseconds 100
        }
        if (-not (Test-Path -LiteralPath $serverReadyPath -PathType Leaf)) {
            $hostOutput = @(Receive-Job -Job $job -Keep 2>&1 | ForEach-Object { $_.ToString() }) -join "`n"
            throw "Mock Host did not start for $Name. $hostOutput"
        }

        $processArguments = @($assemblyPath, "http://127.0.0.1:$port/", $templatePath, $recordsPath, $outputPath)
        $previousErrorActionPreference = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'Continue'
            $processOutput = @(& $dotnetPath @processArguments 2>&1 | ForEach-Object { $_.ToString() })
            $exitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }
        $combinedOutput = $processOutput -join "`n"
        [IO.File]::WriteAllLines($stdoutPath, $processOutput, $utf8)
        [IO.File]::WriteAllText($stderrPath, '', $utf8)

        $null = Assert-RequestLog $requestLogPath $RecordCount $Name
        if ($exitCode -ne $ExpectedExitCode) {
            throw "$Name exited $exitCode; expected $ExpectedExitCode. Output: $combinedOutput"
        }
        if (-not [string]::IsNullOrEmpty($ExpectedError) -and $combinedOutput -notmatch [regex]::Escape($ExpectedError)) {
            throw "$Name did not report the expected validation error '$ExpectedError'. Output: $combinedOutput"
        }
        if ($ExpectedStatus -ne 'completed' -and $combinedOutput -match '(?m)^completed:') {
            throw "$Name printed a completed result after a failure."
        }

        if ($ExpectedStatus -eq 'rejected') {
            if ($PreserveExistingOutput) {
                $actual = [IO.File]::ReadAllBytes($outputPath)
                if ([Convert]::ToBase64String($actual) -ne [Convert]::ToBase64String($sentinel)) {
                    throw "$Name changed the pre-existing output after validation failed."
                }
            }
            elseif ($ExpectNoOutput -and (Test-Path -LiteralPath $outputPath)) {
                throw "$Name left a result ZIP after validation failed."
            }
        }
        else {
            if (-not (Test-Path -LiteralPath $outputPath -PathType Leaf)) {
                throw "$Name did not publish the expected result ZIP."
            }
            $manifest = Read-ArchiveManifest $outputPath
            if ($manifest.status -ne $ExpectedStatus) {
                throw "$Name produced manifest status '$($manifest.status)'; expected '$ExpectedStatus'."
            }
            if ($ExpectedStatus -eq 'completed') {
                if ($manifest.totalRecords -ne 26 -or $manifest.succeeded -ne 26 -or $manifest.failed -ne 0 -or
                    @($manifest.records).Count -ne 26 -or @($manifest.files).Count -ne 52) {
                    throw 'The valid dual-format case has unexpected merged manifest metadata.'
                }
                $archive = [IO.Compression.ZipFile]::OpenRead($outputPath)
                try {
                    foreach ($record in $manifest.records) {
                        $formats = @($record.files | ForEach-Object { $_.format } | Sort-Object -Unique)
                        if (($formats -join ',') -ne 'pdf,svg') {
                            throw "Completed record $($record.index) is missing a requested format."
                        }
                        foreach ($file in $record.files) {
                            $entry = $archive.GetEntry($file.file)
                            if ($null -eq $entry -or $entry.Length -eq 0) {
                                throw "Output ZIP has an absent or empty item '$($file.file)'."
                            }
                        }
                    }
                }
                finally { $archive.Dispose() }
            }
            elseif ($ExpectedStatus -eq 'partial') {
                if ($manifest.succeeded -ne 1 -or $manifest.failed -ne 1 -or @($manifest.files).Count -ne 2 -or
                    @($manifest.errors).Count -ne 1) {
                    throw 'The partial result did not preserve its success, failure, files, and error metadata.'
                }
                $archive = [IO.Compression.ZipFile]::OpenRead($outputPath)
                try {
                    $csvEntry = $archive.GetEntry('errors.csv')
                    if ($null -eq $csvEntry) { throw 'Partial result is missing errors.csv.' }
                    $reader = [IO.StreamReader]::new($csvEntry.Open(), [Text.Encoding]::UTF8)
                    try { $csv = $reader.ReadToEnd() } finally { $reader.Dispose() }
                    if ($csv -notmatch 'BatchRecordFailed') { throw 'Partial errors.csv lost the Host error row.' }
                }
                finally { $archive.Dispose() }
            }
            elseif ($ExpectedStatus -eq 'all_failed') {
                if ($manifest.succeeded -ne 0 -or $manifest.failed -ne 2 -or @($manifest.files).Count -ne 0 -or
                    @($manifest.errors).Count -ne 2) {
                    throw 'The all_failed result unexpectedly required or reported exports.'
                }
            }
        }

        if ($job.State -notin @('Completed', 'Failed')) { Wait-Job -Job $job -Timeout 10 | Out-Null }
        if ($job.State -eq 'Failed') {
            $hostOutput = @(Receive-Job -Job $job -Keep 2>&1 | ForEach-Object { $_.ToString() }) -join "`n"
            throw "Mock Host failed during $Name. $hostOutput"
        }
        return [pscustomobject]@{
            name = $Name; scenario = $Scenario; status = 'PASS'; exitCode = $exitCode
            expectedStatus = $ExpectedStatus; requestLog = $requestLogPath; output = $outputPath
            processOutput = $combinedOutput.Trim()
        }
    }
    finally {
        if ($job.State -notin @('Completed', 'Failed', 'Stopped')) { Stop-Job -Job $job -ErrorAction SilentlyContinue }
        Remove-Job -Job $job -Force -ErrorAction SilentlyContinue
    }
}

$caseResults = [Collections.Generic.List[object]]::new()
$scriptError = $null
try {
    $cases = @(
        @{ Name = 'old-host-reject'; Scenario = 'old-reject'; RecordCount = 1; Exit = 1; Status = 'rejected'; Error = 'ContractFeatureUnavailable'; Preserve = $true; NoOutput = $false }
        @{ Name = 'missing-response-features'; Scenario = 'ignore-header'; RecordCount = 1; Exit = 1; Status = 'rejected'; Error = 'missing required features'; Preserve = $true; NoOutput = $false }
        @{ Name = 'wrong-contract-version'; Scenario = 'wrong-version'; RecordCount = 1; Exit = 1; Status = 'rejected'; Error = 'must declare X-EasyPrint-Contract-Version: 1'; Preserve = $true; NoOutput = $false }
        @{ Name = 'missing-pdf'; Scenario = 'missing-pdf'; RecordCount = 1; Exit = 1; Status = 'rejected'; Error = 'must include non-empty PDF and SVG'; Preserve = $true; NoOutput = $false }
        @{ Name = 'missing-svg'; Scenario = 'missing-svg'; RecordCount = 1; Exit = 1; Status = 'rejected'; Error = 'must include non-empty PDF and SVG'; Preserve = $true; NoOutput = $false }
        @{ Name = 'missing-zip-item'; Scenario = 'missing-entry'; RecordCount = 1; Exit = 1; Status = 'rejected'; Error = 'references missing ZIP item'; Preserve = $false; NoOutput = $true }
        @{ Name = 'empty-export'; Scenario = 'empty-export'; RecordCount = 1; Exit = 1; Status = 'rejected'; Error = 'references empty ZIP item'; Preserve = $true; NoOutput = $false }
        @{ Name = 'missing-record-status'; Scenario = 'missing-status'; RecordCount = 1; Exit = 1; Status = 'rejected'; Error = 'missing its status'; Preserve = $true; NoOutput = $false }
        @{ Name = 'unknown-record-status'; Scenario = 'unknown-status'; RecordCount = 1; Exit = 1; Status = 'rejected'; Error = 'unsupported status'; Preserve = $true; NoOutput = $false }
        @{ Name = 'valid-dual-format-26'; Scenario = 'valid'; RecordCount = 26; Exit = 0; Status = 'completed'; Error = ''; Preserve = $false; NoOutput = $false }
        @{ Name = 'partial'; Scenario = 'partial'; RecordCount = 2; Exit = 1; Status = 'partial'; Error = ''; Preserve = $false; NoOutput = $false }
        @{ Name = 'all-failed'; Scenario = 'all-failed'; RecordCount = 2; Exit = 1; Status = 'all_failed'; Error = ''; Preserve = $false; NoOutput = $false }
    )
    foreach ($case in $cases) {
        $result = Invoke-ConsumerCase -Name $case.Name -Scenario $case.Scenario -RecordCount $case.RecordCount `
            -ExpectedExitCode $case.Exit -ExpectedStatus $case.Status -ExpectedError $case.Error `
            -PreserveExistingOutput $case.Preserve -ExpectNoOutput $case.NoOutput
        $caseResults.Add($result)
        Write-Output "$($case.Name): PASS (exit $($result.exitCode))"
    }
}
catch {
    $scriptError = $_.ToString()
    Write-Output "ERROR: $scriptError"
}

$runtimeList = @(& $dotnetPath --list-runtimes 2>&1 | ForEach-Object { $_.ToString() })
$summary = [ordered]@{
    status = if ($null -eq $scriptError -and $caseResults.Count -eq 12) { 'PASS' } else { 'FAIL' }
    passed = $caseResults.Count
    failed = if ($null -eq $scriptError) { 0 } else { 1 }
    build = if ($NoBuild) { 'REUSED' } else { 'PASS' }
    buildOutput = if ($NoBuild) { @() } else { $buildOutput }
    runtime = [ordered]@{
        dotnetSdk = ((& $dotnetPath --version 2>&1 | ForEach-Object { $_.ToString() }) -join '').Trim()
        dotnetRuntimes = $runtimeList
        powershell = $PSVersionTable.PSVersion.ToString()
        os = [Environment]::OSVersion.VersionString
        targetFramework = 'net6.0'
    }
    template = $templatePath
    softwareEncodableCodeElements = $codeElements.Count
    cases = $caseResults.ToArray()
    error = $scriptError
}
$summaryPath = Join-Path $runPath 'summary.json'
[IO.File]::WriteAllText($summaryPath, (ConvertTo-Json -InputObject $summary -Depth 20), $utf8)
Write-Output "BatchConsumer regression: $($summary.status) ($($summary.passed) passed, $($summary.failed) failed)"
Write-Output "Evidence: $summaryPath"
if ($summary.status -ne 'PASS') { exit 1 }
