param([Parameter(Mandatory = $true)][string]$FrontendPath)
$ErrorActionPreference = 'Stop'
$frontendRoot = (Resolve-Path -LiteralPath $FrontendPath).Path
$proxy = Join-Path $PSScriptRoot '.runtime/proxy.json'
$cli = Join-Path $frontendRoot 'node_modules/@angular/cli/bin/ng.js'
if (!(Test-Path -LiteralPath $proxy)) { throw 'Run python LocalTest/run.py configure (or up) first.' }
if (!(Test-Path -LiteralPath $cli)) { throw 'Run npm install in the frontend first.' }
Push-Location $frontendRoot
try {
    & node $cli serve --configuration simulation --proxy-config $proxy --host 127.0.0.1 --port 4301
    if ($LASTEXITCODE -ne 0) { throw 'Frontend stopped with an error.' }
} finally {
    Pop-Location
}
