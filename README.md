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
