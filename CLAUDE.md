# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A self-paced MSSQL 2022 / T-SQL learning curriculum. There is no application code, build, or test suite — the repo is lesson content (Markdown + SQL) plus a Docker setup for the database the lessons run against. Design specs and per-tier implementation plans live in `docs/superpowers/`.

## Running SQL

The database is MSSQL 2022 Developer in a Docker container named `mssql-learn`, with the AdventureWorks2022 database restored into it. The `sa` password comes from `docker/.env` (gitignored; template in `docker/.env.example`).

```bash
# Start the container
docker compose -f docker/docker-compose.yml up -d

# Run a SQL file / ad-hoc SQL non-interactively (sqlcmd lives inside the container)
cat lessons/NN-topic/setup.sql | docker exec -i mssql-learn /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$SA_PASSWORD" -No
```

The `-No` flag (disable encryption negotiation) is required with the container's sqlcmd. Helper scripts read `SA_PASSWORD` from `docker/.env`. They come in two flavours:

- `scripts/mac/` — bash scripts (macOS/Linux): `connect.sh`, `restore-adventureworks.sh`, `reset-db.sh`
- `scripts/win/` — PowerShell scripts (Windows): `connect.ps1`, `restore-adventureworks.ps1`, `reset-db.ps1`

The .bak is downloaded separately — see `data/adventureworks/README.md`. `reset-db.sh` / `reset-db.ps1` drops and re-restores AdventureWorks, wiping all `lessonNN` schemas.

## Lesson conventions

These are firm conventions from the design spec (`docs/superpowers/specs/2026-05-29-mssql-learning-design.md`); follow them when adding or editing lessons.

- Every lesson directory `lessons/NN-topic/` contains exactly five files: `README.md` (concepts + walkthrough), `setup.sql`, `examples.sql`, `exercises.sql`, `exercises-solutions.sql`.
- **Each lesson owns a dedicated schema named `lessonNN`** (e.g. `lesson17`) inside AdventureWorks2022. `setup.sql` must be **idempotent**: drop all tables in the schema, drop the schema, recreate everything. Lessons needing nothing beyond AdventureWorks still ship a minimal `setup.sql` creating the empty schema.
- Lessons must be runnable end-to-end without the learner designing or seeding their own data — AdventureWorks plus `setup.sql` provides everything.
- `exercises-solutions.sql` has one solution per exercise with a 1–2 line comment explaining the approach, not just the answer.
- Curriculum tiers: 01–04 foundations, 05–08 working with data, 09–11 programming, 12–13 performance, 14–17 internals (storage, optimizer, memory/log, locking/waits). The internals tier has its own design spec (`docs/superpowers/specs/2026-05-30-mssql-internals-tier5-design.md`).
- `cheatsheets/` are standalone reference docs, independent of lesson order.

## Commit style

Conventional-commit prefixes (`feat:`, `fix:`, `docs:`), e.g. `feat: add lesson 17 - locking, blocking, deadlocks and waits`.
