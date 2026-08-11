param(
    [Parameter(Mandatory = $true)]
    [string] $UnityEditor,

    [Parameter(Mandatory = $true)]
    [string] $ProjectRoot,

    [Parameter(Mandatory = $true)]
    [string] $BuildExe,

    [Parameter(Mandatory = $true)]
    [string] $LogFile,

    [string] $Development = "0"
)

$ErrorActionPreference = "Stop"

function Quote-UnityArgument([string] $Value) {
    return '"' + $Value.Replace('"', '\"') + '"'
}

$unityArguments = @(
    "-quit",
    "-projectPath", (Quote-UnityArgument $ProjectRoot),
    "-buildTarget", "Win64",
    "-executeMethod", "FattoPrizzerva.BuildTelemetry.Editor.TelemetryBuild.BuildWindows",
    "-telemetryBuildOutput", (Quote-UnityArgument $BuildExe),
    "-logFile", (Quote-UnityArgument $LogFile)
)

if ($Development -eq "1") {
    $unityArguments += "-telemetryDevelopmentBuild"
}

$logDirectory = Split-Path -Parent $LogFile
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null

Write-Host "Abriendo Unity y preparando la build..."
$startedAt = Get-Date
$process = Start-Process `
    -FilePath $UnityEditor `
    -ArgumentList $unityArguments `
    -WorkingDirectory $ProjectRoot `
    -PassThru

$lastProgress = ""
$lastHeartbeat = [DateTime]::MinValue
$progressPattern = "variants ready|Compiling shader|Building scene|Build completed|Build Finished|Player size statistics|bee_backend"

while (-not $process.HasExited) {
    Start-Sleep -Seconds 5
    $now = Get-Date
    $elapsed = $now - $startedAt
    $progress = $null

    if (Test-Path -LiteralPath $LogFile) {
        $progress = Get-Content -LiteralPath $LogFile -Tail 120 -ErrorAction SilentlyContinue |
            Where-Object { $_ -match $progressPattern } |
            Select-Object -Last 1
    }

    if ($progress -and $progress -ne $lastProgress) {
        Write-Host ("[{0:mm\:ss}] {1}" -f $elapsed, $progress.Trim())
        $lastProgress = $progress
        $lastHeartbeat = $now
    }
    elseif (($now - $lastHeartbeat).TotalSeconds -ge 30) {
        Write-Host ("[{0:mm\:ss}] Unity sigue trabajando..." -f $elapsed)
        $lastHeartbeat = $now
    }
}

$process.WaitForExit()
if ($process.ExitCode -ne 0) {
    Write-Host ""
    Write-Host "Unity ha terminado con error. Ultimas lineas del log:" -ForegroundColor Red
    if (Test-Path -LiteralPath $LogFile) {
        Get-Content -LiteralPath $LogFile -Tail 35
    }
}

exit $process.ExitCode
