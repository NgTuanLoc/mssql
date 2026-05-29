# MSSQL Self-Study Curriculum — Design Spec

**Date:** 2026-05-29
**Author:** brainstorming session with Claude (superpowers:brainstorming)
**Status:** Draft, awaiting user approval

## Goal

A self-paced MSSQL learning project for a learner with **some prior SQL exposure**, taking them from T-SQL fundamentals through to query tuning and execution plans. Practice happens against a real MSSQL 2022 instance running in Docker, using the AdventureWorks sample database.

## Non-Goals

- HA / replication / Always-On Availability Groups
- CLR integration, Service Broker, replication
- Production hardening / backup operations beyond what's needed to restore AdventureWorks
- Cloud-specific topics (Azure SQL DB, Managed Instance) — engine concepts transfer, but Azure-specific tooling is out of scope
- Application-side data access (ORMs, connection pools, etc.)

## User Decisions (captured during brainstorm)

| Decision | Choice |
|---|---|
| Starting level | Some SQL exposure (knows basic SELECT/INSERT, wants proper T-SQL foundation) |
| Material format | Lessons + cheatsheets + exercises |
| Depth ceiling | Through query tuning + execution plans (~13 lessons) |
| Client tool | Azure Data Studio / SSMS (Windows) |
| Dataset | AdventureWorks (Microsoft official) |

## Repository Structure

```
mssql/
├── README.md
├── docker/
│   ├── docker-compose.yml
│   └── .env.example
├── data/
│   └── adventureworks/
│       └── README.md           # how to download & restore the .bak
├── cheatsheets/
│   ├── 00-tsql-syntax.md
│   ├── 01-data-types.md
│   ├── 02-joins-and-sets.md
│   ├── 03-window-functions.md
│   ├── 04-indexes.md
│   └── 05-execution-plans.md
├── lessons/
│   ├── 01-setup-and-basics/
│   │   ├── README.md
│   │   ├── examples.sql
│   │   ├── exercises.sql
│   │   └── exercises-solutions.sql
│   ├── 02-tsql-core-select/
│   ├── 03-data-types-and-expressions/
│   ├── 04-joins-and-sets/
│   ├── 05-aggregations-and-grouping/
│   ├── 06-subqueries-and-ctes/
│   ├── 07-window-functions/
│   ├── 08-dml-insert-update-delete-merge/
│   ├── 09-ddl-and-constraints/
│   ├── 10-views-procedures-functions/
│   ├── 11-transactions-errors-concurrency/
│   ├── 12-indexes/
│   └── 13-execution-plans-and-tuning/
├── backups/                    # AdventureWorks .bak goes here (gitignored)
└── scripts/
    ├── connect.ps1
    ├── restore-adventureworks.ps1
    └── reset-db.ps1
```

### Rationale

- `docker/` separates infra from content.
- `cheatsheets/` are reference docs — useful independent of lesson order.
- `lessons/` is the linear curriculum path; each lesson is self-contained.
- `scripts/` removes repeated PowerShell typing.
- `.env`, `backups/*.bak`, and any user-edited exercise files should be `.gitignore`d; `.env.example` is checked in.

## Docker / MSSQL Environment

### Image and edition

- **Image:** `mcr.microsoft.com/mssql/server:2022-latest`
- **Edition:** Developer (`MSSQL_PID=Developer`) — same engine as Enterprise, free for non-production use; enables advanced features like compression and partitioning if we touch them.

### docker-compose.yml

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

### Persistence model

- **Named volume `mssql-data`** for `/var/opt/mssql` — databases survive `docker compose down`.
- **Bind mount `../backups → /var/opt/mssql/backups`** — drop `AdventureWorks2022.bak` on the host, `RESTORE DATABASE` reads it from inside the container.

### AdventureWorks restore flow

1. Download `AdventureWorks2022.bak` from Microsoft's GitHub release page (URL documented in `data/adventureworks/README.md`).
2. Place the file in `./backups/`.
3. Run `scripts/restore-adventureworks.ps1`, which executes the `RESTORE DATABASE` T-SQL with the correct logical/physical file moves.

### Helper scripts

- `scripts/connect.ps1` — opens an interactive `sqlcmd` session inside the container.
- `scripts/restore-adventureworks.ps1` — one-shot restore from the `.bak`.
- `scripts/reset-db.ps1` — drops and re-restores AdventureWorks for a clean slate.

### Client tools

Documented in `README.md`:
- **SSMS** (Windows-only, full-featured — recommended for plan viewer in lesson 13)
- **Azure Data Studio** (cross-platform, lighter weight)
- Connection: `localhost,1433` with SA credentials.

## Curriculum

13 lessons in 4 tiers. Estimated 2–4 hours/lesson including exercises (~30–50 hours total).

### Tier 1 — Foundations

1. **Setup & First Queries** — Docker up, restore AdventureWorks, connect with SSMS/ADS, first `SELECT`, tour of Object Explorer.
2. **T-SQL Core: SELECT, WHERE, ORDER BY, TOP** — predicates, `LIKE`, `IN`, `BETWEEN`, `NULL` semantics, `TOP n` vs `OFFSET/FETCH`.
3. **Data Types & Expressions** — int/decimal/money, `VARCHAR` vs `NVARCHAR`, `DATE/DATETIME2/DATETIMEOFFSET`, implicit conversion gotchas, `CAST`/`CONVERT`/`TRY_CAST`.
4. **Joins & Set Operations** — INNER/LEFT/RIGHT/FULL/CROSS, multi-table joins, `UNION` vs `UNION ALL`, `INTERSECT`, `EXCEPT`, common join mistakes.

### Tier 2 — Working with Data

5. **Aggregations & Grouping** — `GROUP BY`, `HAVING`, aggregate functions, `GROUPING SETS`, `ROLLUP`, `CUBE`.
6. **Subqueries & CTEs** — scalar/correlated subqueries, `EXISTS` vs `IN`, non-recursive + recursive CTEs.
7. **Window Functions** — `ROW_NUMBER`, `RANK`, `DENSE_RANK`, `LAG`/`LEAD`, `SUM() OVER`, framing clauses.
8. **DML: INSERT / UPDATE / DELETE / MERGE** — `OUTPUT` clause, `MERGE` pitfalls, bulk inserts.

### Tier 3 — Programming MSSQL

9. **DDL & Constraints** — `CREATE TABLE`, PK/FK/UNIQUE/CHECK/DEFAULT, computed columns, schemas, `IDENTITY` vs sequences.
10. **Views, Stored Procedures, Functions** — when to use each, parameters, table-valued vs scalar functions (and the scalar-UDF perf trap).
11. **Transactions, Errors & Concurrency** — `BEGIN/COMMIT/ROLLBACK`, `TRY/CATCH`, `THROW`, isolation levels (RC, RCSI, Snapshot, Serializable), deadlocks demo.

### Tier 4 — Performance

12. **Indexes** — clustered vs nonclustered, included columns, filtered indexes, fragmentation, `sys.dm_db_index_usage_stats`.
13. **Execution Plans & Query Tuning** — estimated vs actual plans, reading operators, statistics, parameter sniffing, SARGability, `SET STATISTICS IO/TIME`, common rewrites.

## Cheatsheet Contents

Format: short intro → tables and code blocks → "common mistakes" footer. Scannable, code-heavy, reference-style.

- **`00-tsql-syntax.md`** — Statement skeletons, built-in function tables (string/date/math/conv), variables, control flow (`IF`, `WHILE`, `CASE`), the `GO` batch separator.
- **`01-data-types.md`** — Every common type with size, range, precision; conversion-matrix gotchas.
- **`02-joins-and-sets.md`** — Text-based diagrams of join types and set ops, with one canonical example each.
- **`03-window-functions.md`** — Every window function with a one-line example; framing-clause cheat (`ROWS` vs `RANGE`, `UNBOUNDED PRECEDING`, etc.).
- **`04-indexes.md`** — Index types, syntax, "what to index" rules of thumb, useful DMVs.
- **`05-execution-plans.md`** — How to read a plan, common operators (Scan/Seek/Hash/Merge/Nested Loops), warning signs, SARGability checklist, useful `SET` commands.

## Per-Lesson Layout

Every lesson directory contains four files:

### `README.md`

```markdown
# Lesson NN — <Title>

## What you'll learn
- bullet
- bullet

## Concepts
<Brief explanation with small inline snippets.>

## Worked examples (AdventureWorks)
<3–6 progressively more interesting queries, each with a 1-line "why".>

## Pitfalls
<2–4 specific traps for this topic.>

## Cheatsheet link
See `cheatsheets/NN-...md`

## Exercises
Open `exercises.sql` and try them in order.
```

### `examples.sql`

Every query from the README, runnable top-to-bottom, with `-- Example N: <description>` comments.

### `exercises.sql`

Numbered prompts as comments, blank space below each:

```sql
-- Exercise 1: Find the top 10 customers by total order value in 2014.
-- Expected columns: CustomerID, TotalSales
-- Your query here:

```

### `exercises-solutions.sql`

One solution per exercise, plus a short comment explaining the approach (not just the answer).

## Success Criteria

A user following this curriculum end-to-end can:

1. Spin up an MSSQL 2022 environment in Docker from this repo with two commands.
2. Restore and query AdventureWorks.
3. Write idiomatic T-SQL across SELECT, DML, DDL, programmability (views/procs/functions), and transactional code.
4. Use window functions and CTEs comfortably.
5. Reason about indexes and read an execution plan well enough to diagnose a slow query.

## Open Questions / Risks

- **AdventureWorks `.bak` download URL** — Microsoft's official location should be linked in `data/adventureworks/README.md` rather than hard-coded into a script, so it can be updated if the URL changes.
- **SSMS vs ADS for the execution-plan lesson** — SSMS has the richer plan viewer; if the user chooses ADS only, lesson 13's plan-reading walkthrough should call out screenshots/steps that differ.
- **Cross-platform PowerShell** — `scripts/*.ps1` assume PowerShell on Windows; cross-platform compatibility is not a goal.

## Out of Scope (Restated)

Anything that requires a second instance, additional services, or features beyond the single Developer-edition container.
