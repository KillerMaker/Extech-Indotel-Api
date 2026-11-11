param([Parameter(Mandatory = $true)][string]$FrontendPath)
$ErrorActionPreference = 'Stop'
$apiRoot = Split-Path $PSScriptRoot -Parent
$frontendRoot = (Resolve-Path -LiteralPath $FrontendPath).Path
$runtime = Join-Path $apiRoot '.simulation'
New-Item -ItemType Directory -Force -Path $runtime | Out-Null
$stateFile = Join-Path $runtime 'processes.json'
if (Test-Path -LiteralPath $stateFile) { throw 'Run Simulation/Stop-Simulation.ps1 before starting again.' }
foreach ($port in @(5188, 5199, 4300)) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue | Where-Object OwningProcess -ne 0) { throw "Port $port is already occupied." }
}
if (!(Test-Path -LiteralPath (Join-Path $frontendRoot 'node_modules/@angular/cli/bin/ng.js'))) { throw 'Run npm install in the frontend first.' }
& dotnet build (Join-Path $apiRoot 'Exatech-Indotel-Simulation.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Simulation build failed.' }
$owned = [System.Collections.Generic.List[object]]::new()
function Launch($name, $executable, $arguments, $directory) {
    $process = Start-Process -FilePath $executable -ArgumentList $arguments -WorkingDirectory $directory -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $runtime "$name.log") -RedirectStandardError (Join-Path $runtime "$name.err.log")
    $owned.Add(@{ Name = $name; Id = $process.Id; StartTime = $process.StartTime.ToUniversalTime().ToString('O') })
    ConvertTo-Json -InputObject $owned.ToArray() | Set-Content -LiteralPath $stateFile
}
$oldEnvironment = $env:ASPNETCORE_ENVIRONMENT
$oldDotnetEnvironment = $env:DOTNET_ENVIRONMENT
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Simulation'
    $env:DOTNET_ENVIRONMENT = 'Simulation'
    Launch 'api' 'dotnet' @('"' + (Join-Path $apiRoot 'bin/Simulation/Release/net10.0/Exatech-Indotel-Simulation.dll') + '"') $apiRoot
    Launch 'frontend' 'node' @('node_modules/@angular/cli/bin/ng.js', 'serve', '--configuration', 'simulation', '--proxy-config', 'proxy.simulation.json', '--host', '127.0.0.1', '--port', '4300') $frontendRoot
} catch {
    & (Join-Path $PSScriptRoot 'Stop-Simulation.ps1')
    throw
} finally {
    $env:ASPNETCORE_ENVIRONMENT = $oldEnvironment
    $env:DOTNET_ENVIRONMENT = $oldDotnetEnvironment
}
try {
    foreach ($url in @('http://127.0.0.1:5199/health', 'http://127.0.0.1:5188/simulation/health', 'http://127.0.0.1:4300/simulation/health')) {
        $ready = $false
        for ($attempt = 0; $attempt -lt 30; $attempt++) {
            try { Invoke-RestMethod -Uri $url -TimeoutSec 1 | Out-Null; $ready = $true; break } catch { Start-Sleep -Milliseconds 500 }
        }
        if (!$ready) { throw "Service not ready: $url. Check logs in $runtime" }
    }
} catch {
    & (Join-Path $PSScriptRoot 'Stop-Simulation.ps1')
    throw
}
Write-Host 'Simulation ready: http://127.0.0.1:4300' 
Write-Host 'Login: sim@example.com / Simulation123!'
Write-Host "Logs: $runtime"
