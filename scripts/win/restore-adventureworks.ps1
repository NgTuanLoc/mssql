<#
.SYNOPSIS
    Restores AdventureWorks2022.bak into the mssql-learn container.
    Place AdventureWorks2022.bak in the ./backups/ directory first.
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

$bakPath = Join-Path $PSScriptRoot "..\backups\AdventureWorks2022.bak"
if (-not (Test-Path $bakPath)) {
    Write-Error "AdventureWorks2022.bak not found at $bakPath. See data/adventureworks/README.md for download instructions."
    exit 1
}

Write-Host "Restoring AdventureWorks2022 -- this takes ~30 seconds..." -ForegroundColor Cyan

$sql = "RESTORE DATABASE [AdventureWorks2022] FROM DISK = N'/var/opt/mssql/backups/AdventureWorks2022.bak' WITH MOVE N'AdventureWorks2022' TO N'/var/opt/mssql/data/AdventureWorks2022.mdf', MOVE N'AdventureWorks2022_log' TO N'/var/opt/mssql/data/AdventureWorks2022_log.ldf', REPLACE, STATS = 10;"

$container = if (Get-Command podman -ErrorAction SilentlyContinue) { "podman" } else { "docker" }
$sql | & $container exec -i mssql-learn /opt/mssql-tools18/bin/sqlcmd `
    -S localhost -U sa -P $password -No

if ($LASTEXITCODE -eq 0) {
    Write-Host "Restore complete." -ForegroundColor Green
} else {
    Write-Error "Restore failed (exit code $LASTEXITCODE)."
    exit $LASTEXITCODE
}
