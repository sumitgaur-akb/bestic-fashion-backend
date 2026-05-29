param(
    [switch]$AllDotnet
)

$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$ports = @(5000, 5001, 5197)

foreach ($port in $ports) {
    Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique |
        ForEach-Object {
            if ($_ -and $_ -ne $PID) {
                Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue
            }
        }
}

try {
    Get-CimInstance Win32_Process -Filter "name = 'dotnet.exe'" |
        Where-Object {
            $_.CommandLine -like "*$projectRoot*" -or
            $_.CommandLine -like "*FlipShop.Api*"
        } |
        ForEach-Object {
            Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
        }
}
catch {
    Write-Warning "Could not inspect dotnet command lines. Ports were stopped, but another dotnet process may still be locking build files."
    Write-Warning "If build is still locked, run: .\scripts\stop-local.ps1 -AllDotnet"
}

if ($AllDotnet) {
    Get-Process dotnet -ErrorAction SilentlyContinue |
        ForEach-Object {
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
        }
}
