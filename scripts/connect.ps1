<#
.SYNOPSIS
    Opens an interactive sqlcmd session in the mssql-learn container.
#>
$envFile = Join-Path $PSScriptRoot "..\docker\.env"
foreach ($line in Get-Content $envFile) {
    if ($line -match '^([^=#]+)=(.*)$') {
        [System.Environment]::SetEnvironmentVariable($Matches[1].Trim(), $Matches[2].Trim(), 'Process')
    }
}

$password = $env:SA_PASSWORD
if (-not $password) {
    Write-Error "SA_PASSWORD not found in $envFile"
    exit 1
}

Write-Host "Connecting to mssql-learn as sa..." -ForegroundColor Cyan
docker exec -it mssql-learn /opt/mssql-tools18/bin/sqlcmd `
    -S localhost -U sa -P $password -No
