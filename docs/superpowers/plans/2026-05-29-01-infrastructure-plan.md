# Infrastructure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the Docker/MSSQL 2022 environment and helper scripts so a learner can go from zero to a live AdventureWorks query with two commands.

**Architecture:** Single `docker-compose.yml` runs `mcr.microsoft.com/mssql/server:2022-latest` (Developer edition). A named volume (`mssql-data`) persists the database across `docker compose down`. A bind-mount exposes `./backups/` inside the container at `/var/opt/mssql/backups/` so `RESTORE DATABASE` can read the `.bak` without copying. Three PowerShell scripts wrap the common operations; each loads credentials from `docker/.env` at runtime.

**Tech Stack:** Docker Compose v2, MSSQL 2022 Developer, PowerShell 7+, `sqlcmd` (at `/opt/mssql-tools18/bin/sqlcmd` inside the container).

---

## File Map

| Action | Path | Purpose |
|--------|------|---------|
| Create | `docker/docker-compose.yml` | Container definition |
| Create | `docker/.env.example` | Credential template (checked in) |
| Create | `scripts/connect.ps1` | Opens interactive sqlcmd session |
| Create | `scripts/restore-adventureworks.ps1` | Restores AdventureWorks from `.bak` |
| Create | `scripts/reset-db.ps1` | Drops + re-restores AdventureWorks |
| Create | `data/adventureworks/README.md` | Download instructions for the `.bak` |
| Create | `README.md` | Project overview and quick-start |

---

## Task 1: Docker Compose config

**Files:**
- Create: `docker/docker-compose.yml`
- Create: `docker/.env.example`

- [ ] **Step 1: Create `docker/docker-compose.yml`**

```yaml
services:
  mssql:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: mssql-learn
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "${SA_PASSWORD}"
      MSSQL_PID: "Developer"
    ports:
      - "1433:1433"
    volumes:
      - mssql-data:/var/opt/mssql
      - ../backups:/var/opt/mssql/backups
volumes:
  mssql-data:
```

- [ ] **Step 2: Create `docker/.env.example`**

```
SA_PASSWORD=YourStr0ngP@ssword!
```

- [ ] **Step 3: Copy `.env.example` to `.env` and set a real password**

```powershell
Copy-Item docker/.env.example docker/.env
# Edit docker/.env and replace the placeholder password
notepad docker/.env
```

- [ ] **Step 4: Start the container**

```powershell
docker compose -f docker/docker-compose.yml up -d
```

Expected output: container `mssql-learn` starts. `docker ps` shows it running on port 1433.

- [ ] **Step 5: Wait for MSSQL to be ready (~15–20 s), then smoke-test**

```powershell
docker exec mssql-learn /opt/mssql-tools18/bin/sqlcmd `
  -S localhost -U sa -P "YourStr0ngP@ssword!" -No `
  -Q "SELECT @@VERSION"
```

Expected: prints SQL Server 2022 version string.

- [ ] **Step 6: Commit**

```powershell
git add docker/docker-compose.yml docker/.env.example
git commit -m "feat: add docker-compose for MSSQL 2022 Developer"
```

---

## Task 2: `scripts/connect.ps1`

**Files:**
- Create: `scripts/connect.ps1`

- [ ] **Step 1: Create `scripts/connect.ps1`**

```powershell
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
```

- [ ] **Step 2: Verify the script opens an interactive session**

```powershell
.\scripts\connect.ps1
```

Expected: `1>` sqlcmd prompt appears. Type `SELECT 1 AS ok; GO` and verify it returns `ok = 1`. Type `EXIT` to quit.

- [ ] **Step 3: Commit**

```powershell
git add scripts/connect.ps1
git commit -m "feat: add connect.ps1 helper script"
```

---

## Task 3: `scripts/restore-adventureworks.ps1`

**Files:**
- Create: `scripts/restore-adventureworks.ps1`

- [ ] **Step 1: Create `scripts/restore-adventureworks.ps1`**

```powershell
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

Write-Host "Restoring AdventureWorks2022 — this takes ~30 seconds..." -ForegroundColor Cyan

$sql = @'
RESTORE DATABASE [AdventureWorks2022]
FROM DISK = N'/var/opt/mssql/backups/AdventureWorks2022.bak'
WITH
    MOVE N'AdventureWorks2022'     TO N'/var/opt/mssql/data/AdventureWorks2022.mdf',
    MOVE N'AdventureWorks2022_log' TO N'/var/opt/mssql/data/AdventureWorks2022_log.ldf',
    REPLACE,
    STATS = 10;
'@

docker exec mssql-learn /opt/mssql-tools18/bin/sqlcmd `
    -S localhost -U sa -P $password -No `
    -Q $sql

if ($LASTEXITCODE -eq 0) {
    Write-Host "Restore complete." -ForegroundColor Green
} else {
    Write-Error "Restore failed (exit code $LASTEXITCODE)."
    exit $LASTEXITCODE
}
```

- [ ] **Step 2: Download the `.bak` and place it in `backups/`**

See `data/adventureworks/README.md` for the download URL. The file must be named exactly `AdventureWorks2022.bak`.

- [ ] **Step 3: Run the restore script**

```powershell
.\scripts\restore-adventureworks.ps1
```

Expected: progress messages like `10 percent processed.` … `Restore complete.`

- [ ] **Step 4: Verify the database is accessible**

```powershell
docker exec mssql-learn /opt/mssql-tools18/bin/sqlcmd `
  -S localhost -U sa -P "YourStr0ngP@ssword!" -No `
  -Q "SELECT TOP 3 FirstName, LastName FROM AdventureWorks2022.Person.Person"
```

Expected: three rows of person names.

- [ ] **Step 5: Commit**

```powershell
git add scripts/restore-adventureworks.ps1
git commit -m "feat: add restore-adventureworks.ps1 helper script"
```

---

## Task 4: `scripts/reset-db.ps1`

**Files:**
- Create: `scripts/reset-db.ps1`

- [ ] **Step 1: Create `scripts/reset-db.ps1`**

```powershell
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
```

- [ ] **Step 2: Verify reset works end-to-end**

```powershell
# Create some dummy data to prove it gets wiped
docker exec mssql-learn /opt/mssql-tools18/bin/sqlcmd `
  -S localhost -U sa -P "YourStr0ngP@ssword!" -No `
  -Q "USE AdventureWorks2022; CREATE TABLE dbo._test_reset (id INT); INSERT dbo._test_reset VALUES (1)"

# Reset
.\scripts\reset-db.ps1

# Verify the dummy table is gone
docker exec mssql-learn /opt/mssql-tools18/bin/sqlcmd `
  -S localhost -U sa -P "YourStr0ngP@ssword!" -No `
  -Q "USE AdventureWorks2022; SELECT OBJECT_ID('dbo._test_reset') AS ShouldBeNull"
```

Expected: `ShouldBeNull` column shows `NULL`.

- [ ] **Step 3: Commit**

```powershell
git add scripts/reset-db.ps1
git commit -m "feat: add reset-db.ps1 helper script"
```

---

## Task 5: `data/adventureworks/README.md`

**Files:**
- Create: `data/adventureworks/README.md`

- [ ] **Step 1: Create `data/adventureworks/README.md`**

```markdown
# AdventureWorks Download Instructions

The AdventureWorks2022 backup file is not included in this repository (it is ~200 MB and gitignored).

## Download

1. Go to the Microsoft SQL Server Samples releases page:
   https://github.com/Microsoft/sql-server-samples/releases/tag/adventureworks

2. Download **`AdventureWorks2022.bak`** (the full OLTP database, ~200 MB).

## Install

3. Place the downloaded file in the `backups/` directory at the root of this repo:

   ```
   mssql/
   └── backups/
       └── AdventureWorks2022.bak   ← here
   ```

4. Run the restore script (container must already be running):

   ```powershell
   .\scripts\restore-adventureworks.ps1
   ```

5. Verify:

   ```powershell
   .\scripts\connect.ps1
   ```

   Then in the sqlcmd prompt:

   ```sql
   SELECT TOP 3 FirstName, LastName FROM AdventureWorks2022.Person.Person;
   GO
   ```

## Re-seeding

If you make changes you want to undo, run:

```powershell
.\scripts\reset-db.ps1
```

This drops and re-restores AdventureWorks (including any lesson schemas you created).
```

- [ ] **Step 2: Commit**

```powershell
git add data/adventureworks/README.md
git commit -m "docs: add AdventureWorks download instructions"
```

---

## Task 6: Root `README.md`

**Files:**
- Create: `README.md`

- [ ] **Step 1: Create `README.md`**

```markdown
# MSSQL Self-Study

A self-paced T-SQL curriculum using MSSQL 2022 Developer edition in Docker and the AdventureWorks sample database. Covers T-SQL fundamentals through execution plan reading and query tuning (~13 lessons, ~30–50 hours).

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (with Compose v2)
- PowerShell 7+
- [SSMS](https://learn.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms) (recommended) or [Azure Data Studio](https://azure.microsoft.com/en-us/products/data-studio)

## Quick Start

```powershell
# 1. Copy the env template and set a strong password
Copy-Item docker/.env.example docker/.env
notepad docker/.env

# 2. Start the container
docker compose -f docker/docker-compose.yml up -d

# 3. Download AdventureWorks2022.bak — see data/adventureworks/README.md
# 4. Restore
.\scripts\restore-adventureworks.ps1
```

Connect in SSMS / Azure Data Studio: `localhost,1433` · Login: `sa` · Password: (from your `.env`)

## Curriculum

| Tier | Lessons | Topics |
|------|---------|--------|
| 1 — Foundations | 01–04 | Setup, SELECT/WHERE/ORDER BY, Data Types, Joins & Sets |
| 2 — Working with Data | 05–08 | Aggregations, Subqueries/CTEs, Window Functions, DML |
| 3 — Programming MSSQL | 09–11 | DDL/Constraints, Views/Procs/Functions, Transactions/Concurrency |
| 4 — Performance | 12–13 | Indexes, Execution Plans & Query Tuning |

Each lesson directory contains `README.md` (concepts + examples), `setup.sql` (run once), `exercises.sql`, and `exercises-solutions.sql`.

## Helper Scripts

| Script | Purpose |
|--------|---------|
| `scripts/connect.ps1` | Open an interactive sqlcmd session |
| `scripts/restore-adventureworks.ps1` | Restore AdventureWorks from the `.bak` |
| `scripts/reset-db.ps1` | Drop and re-restore AdventureWorks (clean slate) |

## Reference

- `cheatsheets/` — standalone T-SQL reference docs (syntax, data types, joins, window functions, indexes, execution plans)
- `data/adventureworks/README.md` — how to download the sample database
```

- [ ] **Step 2: Commit**

```powershell
git add README.md
git commit -m "docs: add root README with quick-start guide"
```

---

## Self-Review

**Spec coverage check:**

| Spec requirement | Covered in |
|---|---|
| `docker/docker-compose.yml` with Developer edition, named volume, bind-mount | Task 1 |
| `docker/.env.example` with SA_PASSWORD | Task 1 |
| `scripts/connect.ps1` | Task 2 |
| `scripts/restore-adventureworks.ps1` | Task 3 |
| `scripts/reset-db.ps1` | Task 4 |
| `data/adventureworks/README.md` with download URL | Task 5 |
| Root `README.md` with prerequisites, quick-start, connection info, curriculum overview | Task 6 |
| `backups/` directory gitignored | `.gitignore` already handles this (pre-existing) |

All spec requirements are covered. No placeholders remain.
