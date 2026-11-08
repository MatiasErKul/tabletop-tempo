<#
.SYNOPSIS
  Batch-mode wrapper for the Tabletop Tempo Unity project.

.EXAMPLE
  ./tools/unity.ps1 compile            # open project headless, fail on compile errors
  ./tools/unity.ps1 setup              # configure Quest/XR/URP settings + rebuild scenes
  ./tools/unity.ps1 test               # EditMode + PlayMode tests
  ./tools/unity.ps1 test-edit          # EditMode tests only
  ./tools/unity.ps1 method -Method TabletopTempo.EditorTools.BatchEntry.SetupProject
  ./tools/unity.ps1 build              # Builds/TabletopTempo.apk

  The Unity Editor must NOT have this project open at the same time.
  The editor path is read from tools/unity-path.txt.
#>
param(
    [ValidateSet('compile', 'setup', 'test', 'test-edit', 'test-play', 'build', 'build-dev', 'method')]
    [string]$Mode = 'compile',
    [string]$Method = '',
    [string]$TestFilter = '',
    [int]$TimeoutMinutes = 30
)

$ErrorActionPreference = 'Stop'
$repo    = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo 'TabletopTempo'
$logDir  = Join-Path $repo 'Logs'
$resDir  = Join-Path $repo 'TestResults'
$unity   = (Get-Content (Join-Path $PSScriptRoot 'unity-path.txt') -TotalCount 1).Trim()

if (-not (Test-Path $unity)) { throw "Unity not found at '$unity'. Fix tools/unity-path.txt." }
New-Item -ItemType Directory -Force $logDir, $resDir | Out-Null

function Quote([string]$s) { if ($s -match '\s') { '"' + $s + '"' } else { $s } }

function Invoke-Unity {
    param([string[]]$UnityArgs, [string]$Name, [switch]$NoQuit)
    $log = Join-Path $logDir "$Name.log"
    $all = @('-batchmode', '-nographics', '-projectPath', $project, '-logFile', $log)
    if (-not $NoQuit) { $all += '-quit' }
    $all += $UnityArgs
    $argLine = ($all | ForEach-Object { Quote $_ }) -join ' '
    Write-Host ">> Unity $Name  (log: Logs/$Name.log)"
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $p = Start-Process -FilePath $unity -ArgumentList $argLine -PassThru -NoNewWindow
    # Touch the handle now: otherwise .ExitCode is $null after WaitForExit(timeout) and failures look like success.
    $null = $p.Handle
    # Hard timeout so an async batch method that never calls Exit cannot hang forever.
    if (-not $p.WaitForExit($TimeoutMinutes * 60 * 1000)) {
        Write-Host "   TIMEOUT after $TimeoutMinutes min, killing Unity"
        try { $p.Kill() } catch {}
        $p.WaitForExit()
        $sw.Stop()
        return 124
    }
    $sw.Stop()
    $code = $p.ExitCode
    Write-Host ("   exit {0} after {1:n0}s" -f $code, $sw.Elapsed.TotalSeconds)
    if (Test-Path $log) {
        $errs = Select-String -Path $log -Pattern 'error CS\d+|Scripts have compiler errors|Exception:|BuildFailedException|\[TT-ERROR\]' -SimpleMatch:$false |
                Select-Object -First 40
        if ($errs) { Write-Host '   --- first errors in log ---'; $errs | ForEach-Object { Write-Host ('   ' + $_.Line.Trim()) } }
    }
    return $code
}

function Show-TestResults([string]$xmlPath) {
    if (-not (Test-Path $xmlPath)) { Write-Host "   (no results file: $xmlPath)"; return $false }
    [xml]$x = Get-Content $xmlPath
    $run = $x.'test-run'
    Write-Host ("   tests: total {0}, passed {1}, failed {2}, skipped {3}" -f $run.total, $run.passed, $run.failed, $run.skipped)
    $failed = $x.SelectNodes("//test-case[@result='Failed']")
    foreach ($f in $failed) {
        Write-Host ("   FAIL {0}" -f $f.fullname)
        $msg = $f.failure.message.'#cdata-section'
        if ($msg) { Write-Host ('        ' + ($msg -split "`n")[0]) }
    }
    return ([int]$run.failed -eq 0)
}

function Run-Tests([string]$platform) {
    $xml = Join-Path $resDir "$platform.xml"
    if (Test-Path $xml) { Remove-Item $xml -Force }
    $testArgs = @('-runTests', '-testPlatform', $platform, '-testResults', $xml)
    if ($TestFilter) { $testArgs += @('-testFilter', $TestFilter) }
    # -runTests quits on its own; passing -quit as well aborts the run.
    $code = Invoke-Unity -UnityArgs $testArgs -Name "tests-$platform" -NoQuit
    $ok = Show-TestResults $xml
    return ($ok -and ($code -eq 0))
}

switch ($Mode) {
    'compile'   { exit (Invoke-Unity -UnityArgs @() -Name 'compile') }
    # Async: BatchEntry.SetupProject exits the editor itself, so no -quit.
    'setup'     { exit (Invoke-Unity -UnityArgs @('-executeMethod', 'TabletopTempo.EditorTools.BatchEntry.SetupProject') -Name 'setup' -NoQuit) }
    'method'    {
        if (-not $Method) { throw 'Pass -Method Namespace.Class.Method' }
        exit (Invoke-Unity -UnityArgs @('-executeMethod', $Method) -Name ('method-' + ($Method -split '\.')[-1]))
    }
    'build'     { exit (Invoke-Unity -UnityArgs @('-buildTarget', 'Android', '-executeMethod', 'TabletopTempo.EditorTools.BuildQuest.BuildRelease') -Name 'build') }
    'build-dev' { exit (Invoke-Unity -UnityArgs @('-buildTarget', 'Android', '-executeMethod', 'TabletopTempo.EditorTools.BuildQuest.BuildDevelopment') -Name 'build-dev') }
    'test-edit' { if (Run-Tests 'EditMode') { exit 0 } else { exit 1 } }
    'test-play' { if (Run-Tests 'PlayMode') { exit 0 } else { exit 1 } }
    'test'      {
        $a = Run-Tests 'EditMode'
        $b = Run-Tests 'PlayMode'
        if ($a -and $b) { exit 0 } else { exit 1 }
    }
}
