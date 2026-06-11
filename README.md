# MSSQL Self-Study

A self-paced T-SQL curriculum using MSSQL 2022 Developer edition in Docker and the AdventureWorks sample database. Covers T-SQL fundamentals through execution plan reading and query tuning (~13 lessons, ~30–50 hours).

## Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (with Compose v2)
- [SSMS](https://learn.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms) (recommended) or [Azure Data Studio](https://azure.microsoft.com/en-us/products/data-studio)
- **Windows only:** PowerShell 7+

## Quick Start

**macOS / Linux:**
```bash
# 1. Copy the env template and set a strong password
cp docker/.env.example docker/.env
nano docker/.env

# 2. Start the container
docker compose -f docker/docker-compose.yml up -d

# 3. Download AdventureWorks2022.bak — see data/adventureworks/README.md
# 4. Restore
./scripts/mac/restore-adventureworks.sh
```

**Windows (PowerShell):**
```powershell
# 1. Copy the env template and set a strong password
Copy-Item docker/.env.example docker/.env
notepad docker/.env

# 2. Start the container
docker compose -f docker/docker-compose.yml up -d

# 3. Download AdventureWorks2022.bak — see data/adventureworks/README.md
# 4. Restore
.\scripts\win\restore-adventureworks.ps1
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

Scripts live in `scripts/mac/` (bash) and `scripts/win/` (PowerShell).

| Script | macOS / Linux | Windows |
|--------|--------------|---------|
| Open interactive sqlcmd session | `scripts/mac/connect.sh` | `scripts\win\connect.ps1` |
| Restore AdventureWorks from `.bak` | `scripts/mac/restore-adventureworks.sh` | `scripts\win\restore-adventureworks.ps1` |
| Drop and re-restore (clean slate) | `scripts/mac/reset-db.sh` | `scripts\win\reset-db.ps1` |

## Reference

- `cheatsheets/` — standalone T-SQL reference docs (how MSSQL works, syntax, data types, joins, window functions, indexes, execution plans). Start with `00-how-mssql-works.md` for the architecture, SQL logical execution order, and plan-reading mental model.
- `data/adventureworks/README.md` — how to download the sample database
