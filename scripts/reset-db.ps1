<#
.SYNOPSIS
    Drops AdventureWorks2022 (and all lesson schemas inside it) then re-restores
    from the backup. Use this to recover from any data corruption or mistakes.
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

Write-Host "Dropping AdventureWorks2022..." -ForegroundColor Yellow

$dropSql = @'
IF DB_ID('AdventureWorks2022') IS NOT NULL
BEGIN
    ALTER DATABASE [AdventureWorks2022] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [AdventureWorks2022];
    PRINT 'Database dropped.';
END
ELSE
    PRINT 'Database did not exist, skipping drop.';
'@

docker exec mssql-learn /opt/mssql-tools18/bin/sqlcmd `
    -S localhost -U sa -P $password -No `
    -Q $dropSql

if ($LASTEXITCODE -ne 0) {
    Write-Error "Drop failed."
    exit 1
}

Write-Host "Re-restoring AdventureWorks2022..." -ForegroundColor Cyan
& "$PSScriptRoot\restore-adventureworks.ps1"
