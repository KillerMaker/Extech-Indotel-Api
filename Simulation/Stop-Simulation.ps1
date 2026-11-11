$ErrorActionPreference = 'Stop'
$stateFile = Join-Path (Split-Path $PSScriptRoot -Parent) '.simulation/processes.json'
if (!(Test-Path -LiteralPath $stateFile)) { Write-Host 'Simulation is not running.'; exit }
$owned = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json
foreach ($entry in @($owned) | Sort-Object -Property Name -Descending) {
    $process = Get-Process -Id $entry.Id -ErrorAction SilentlyContinue
    if ($process -and $process.StartTime.ToUniversalTime().Ticks -eq ([datetime]$entry.StartTime).ToUniversalTime().Ticks) {
        # Windows PowerShell 5.1 does not support Process.Kill(entireProcessTree).
        & taskkill.exe /PID $process.Id /T /F | Out-Null
        if ($LASTEXITCODE -ne 0 -and (Get-Process -Id $entry.Id -ErrorAction SilentlyContinue)) {
            throw "Could not stop simulation process $($entry.Name) (PID $($entry.Id))."
        }
        $process.WaitForExit(5000) | Out-Null
    }
}
Remove-Item -LiteralPath $stateFile
Write-Host 'Simulation stopped. Temporary database and mock state have been discarded.'
