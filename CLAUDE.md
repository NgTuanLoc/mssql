# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A self-paced MSSQL 2022 / T-SQL learning curriculum: lesson content (Jupyter notebooks + SQL) plus a Docker setup for the database the lessons run against. The T-SQL lessons have no application code, build, or test suite. The separate EF Core practice track in `efcore/` is a .NET 10 xUnit project (see "EF Core track" below). Design specs and implementation plans live in `docs/superpowers/`.

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

Notebook lessons (`.ipynb`) are run in Azure Data Studio: open the notebook, attach the SQL kernel to the saved `localhost` connection for the `mssql-learn` container, and run cells top to bottom. To verify a notebook's SQL non-interactively, extract its code-cell sources joined with `GO` and pipe them through the same `sqlcmd` invocation shown above.

## Lesson conventions

These are firm conventions from the design spec (`docs/superpowers/specs/2026-05-29-mssql-learning-design.md`); follow them when adding or editing lessons.

- Lessons use the notebook format (design spec: `docs/superpowers/specs/2026-07-21-notebook-lesson-format-design.md`; all lessons 01–18 are converted). A lesson directory contains exactly four files: `setup.sql`, `lesson.ipynb` (concepts + runnable examples, merged), `exercises.ipynb` (prompts only), `exercises-solutions.ipynb` (same prompts + worked solutions). The normative rules governing a conversion — file mapping, verbatim transfer, batching, `-- [manual]` cells, and the verification harness — live in `docs/superpowers/plans/2026-07-21-notebook-rollout-recipe.md`.
- Notebooks target Azure Data Studio's native SQL kernel (`kernelspec` name `SQL`). First code cell is `USE AdventureWorks2022;`. Commit notebooks with all cell outputs cleared. Never embed connection details or credentials in a notebook. SSMS remains the tool for graphical-plan-viewer-dependent steps; notebooks call this out inline where needed.
- **Each lesson owns a dedicated schema named `lessonNN`** (e.g. `lesson17`) inside AdventureWorks2022. `setup.sql` must be **idempotent**: drop all tables in the schema, drop the schema, recreate everything. Lessons needing nothing beyond AdventureWorks still ship a minimal `setup.sql` creating the empty schema.
- Lessons must be runnable end-to-end without the learner designing or seeding their own data — AdventureWorks plus `setup.sql` provides everything.
- `exercises-solutions.ipynb` has one solution per exercise, each followed by a short `**Why:**` markdown note explaining the approach, not just the answer.
- Curriculum tiers: 01–04 foundations, 05–08 working with data, 09–11 programming, 12–13 performance, 14–17 internals (storage, optimizer, memory/log, locking/waits), 18 capstone (complex queries). The internals tier has its own design spec (`docs/superpowers/specs/2026-05-30-mssql-internals-tier5-design.md`); the capstone's is `docs/superpowers/specs/2026-06-11-complex-queries-lesson-design.md`.
- `cheatsheets/` are standalone reference docs, independent of lesson order.

## EF Core track

A separate practice track in `efcore/` (spec: `docs/superpowers/specs/2026-10-10-efcore-track-design.md`): one xUnit project, `efcore/EfCoreLessons.csproj`, on .NET 10 and EF Core 10.0.12. Package versions are pinned exactly; `efcore/global.json` pins the SDK to 10.0.x and `efcore/dotnet-tools.json` pins `dotnet-ef`.

```bash
cd efcore
dotnet test --filter "FullyQualifiedName!~Exercises"   # examples + solutions + harness self-tests: must show 0 failed and 0 skipped (a NotImplementedException solution reports as skipped)
dotnet test --filter E01Exercises                       # the learner's loop: unattempted exercises report as skipped
```

- Lessons live in `efcore/lessons/E<NN>-<slug>/`: `README.md`, `Examples.cs`, `Checks.cs`, `Exercises.cs`, `Solutions.cs`, plus `E<NN>Context.cs` when the lesson changes the model. Abstract `E<NN>Checks` holds one `[Exercise]` test per exercise and an abstract method for each. `E<NN>Exercises` implements them as `throw new NotImplementedException()` (reported as skipped) and `E<NN>Solutions` with the answers. Prompts are identical in both files, and each solution is followed by a `// Why:` comment.
- 5 exercises (`Ex1`–`Ex5`) and 3–4 challenges (`Ch1`–`Ch4`) per lesson. Checks assert behavior where it is the point (round trips via `Sql.AssertRoundTrips`, SQL shape, change-tracker state) and compare rows with a reference T-SQL query via `Reference.Rows<T>` where practical.
- `efcore/Lab/` gives every test a connection and transaction that is rolled back, so the track never modifies AdventureWorks and needs no `setup.sql`. Code-first lessons (E10–E12) will use `EfLab_<lesson>` databases instead.
- `Lab/QueryPlan.cs`: `QueryPlan.OperatorsAsync(db, query)` returns the estimated plan's physical operators (`SET SHOWPLAN_XML`, nothing executes, not counted in `Sql`), for checks that assert seek vs scan. E05 runs on `E05Context` (lazy-loading proxies on), so a missed load shows up as an extra round trip; checks there never pass entities to a formatting assertion, because formatting a proxy lazy-loads.
- `efcore/Model/` is scaffolder output (command in `efcore/README.md`). Never hand-edit it; lesson-specific model changes go in a derived context in the lesson folder.
- EF can't translate through a positional record's constructor: compose (`OrderBy`, set operations) on anonymous types and project into the record last.
- A lesson is done when the build has no new warnings, its examples and solutions pass, its exercises all report as skipped, and every EF Core 10-specific claim has been checked against the docs.

## Commit style

Conventional-commit prefixes (`feat:`, `fix:`, `docs:`), e.g. `feat: add lesson 17 - locking, blocking, deadlocks and waits`.
