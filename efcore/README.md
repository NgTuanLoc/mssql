# EF Core Practice Track

A practice track for **EF Core 10** on the same Docker SQL Server and AdventureWorks2022 database as the T-SQL lessons. It's aimed at people who have used EF Core and want depth: predicting the SQL it generates, avoiding loading and performance traps, controlling change tracking and concurrency, and designing and migrating models.

Exercises are xUnit tests. Each one checks **how** your query ran (round trips, the shape of the generated SQL, change-tracker state), not just the rows it returned.

## Prerequisites

- The `mssql-learn` container running, with AdventureWorks2022 restored (see the [root README](../README.md))
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). `global.json` pins 10.0.x, so an installed .NET 11 preview SDK is ignored here.
- An editor with a test runner (Visual Studio, Rider, or VS Code with C# Dev Kit), or just the CLI

The sa password is read from the `SA_PASSWORD` environment variable, falling back to `docker/.env`, the same file the helper scripts use. Nothing to configure if the container already works.

## First run

From this folder:

```
dotnet tool restore                  # local dotnet-ef 10.0.12 (see dotnet-tools.json)
dotnet test --filter E01Examples     # should pass
```

## The practice loop

Each lesson in `lessons/` has five files:

| File | What it is |
|---|---|
| `README.md` | concepts, pitfalls, links to the T-SQL lessons it builds on |
| `Examples.cs` | worked examples, as tests that pass and print their SQL |
| `Checks.cs` | the assertions for each exercise (read them, they're the spec) |
| `Exercises.cs` | **the only file you edit**: replace each `throw new NotImplementedException()` |
| `Solutions.cs` | worked answers, each with a `Why:` note; peek after attempting |

```
dotnet test --filter E01Exercises    # skipped = not attempted, failed = wrong, passed = done
dotnet test --filter E01Examples --logger "console;verbosity=detailed"   # examples with their SQL
dotnet test --filter "FullyQualifiedName!~Exercises"                     # self-check: all examples + solutions
```

Every exercise check prints the SQL your code sent, so a failing round-trip check shows you the N+1 directly. Keep `Exercises.cs` compiling: one half-written exercise that doesn't build stops the whole project's tests from running, so comment it out until it compiles.

## How the database is used

- **AdventureWorks2022 is never modified.** Each test runs on its own connection inside a transaction that is rolled back when the test ends (a commit is refused with an error), so writes, updates and deletes in exercises disappear afterwards. No setup scripts, and tests can run in any order.
- **Some lessons open a second session** (E09: blocking, isolation levels) with its own connection and transaction, also rolled back. Every lab connection waits at most 3 seconds for a lock (`SET LOCK_TIMEOUT 3000`), so a test blocked by its own second session fails instead of hanging. E09's examples and solutions wait out a few of these timeouts on purpose.
- **The code-first lessons (E10–E12) use their own `EfLab_<lesson>` databases**, created on first use. E10 and E11 create each test's tables inside a rolled-back transaction, so their databases stay empty; E12 drops and recreates `EfLab_E12` for every test because migrations commit. The harness refuses to create, drop or migrate any database whose name doesn't start with `EfLab_`, so AdventureWorks can't be dropped by accident. The `EfLab_*` databases are disposable: delete them whenever you like (`reset-db` doesn't touch them).
- Tests run one at a time (`xunit.runner.json`), which keeps SQL capture and blocking demos predictable.

## Lessons

| # | Lesson | Status |
|---|---|---|
| E01 | Setup & seeing the SQL | available |
| E02 | Projections, joins & nulls | available |
| E03 | Grouping, subqueries & where LINQ runs out | available |
| E04 | Raw SQL, views, functions & procedures | available |
| E05 | Loading related data | available |
| E06 | Query performance | available |
| E07 | Change tracking internals | available |
| E08 | SaveChanges & set-based writes | available |
| E09 | Concurrency & transactions | available |
| E10 | Modeling with the Fluent API | available |
| E11 | Inheritance & JSON columns | available |
| E12 | Migrations workflow (uses `dotnet ef`) | available |
| E13 | Capstone | planned |

Design: [`docs/superpowers/specs/2026-10-10-efcore-track-design.md`](../docs/superpowers/specs/2026-10-10-efcore-track-design.md).

## The model

`Model/` is the AdventureWorks model, generated once and committed. It is never edited by hand. Shared corrections go in partial classes outside `Model/`, and lesson-specific changes go in a context derived from `AdventureWorksContext` (see `lessons/E04-*/E04Context.cs`). To regenerate it, from this folder:

```bash
# macOS / Linux
SA_PASSWORD=$(grep '^SA_PASSWORD=' ../docker/.env | cut -d= -f2- | tr -d '\r')
dotnet ef dbcontext scaffold \
  "Server=localhost,1433;Database=AdventureWorks2022;User Id=sa;Password=$SA_PASSWORD;TrustServerCertificate=True" \
  Microsoft.EntityFrameworkCore.SqlServer \
  --output-dir Model --context AdventureWorksContext --no-onconfiguring --force \
  --schema Person --schema HumanResources --schema Production \
  --schema Purchasing --schema Sales --schema dbo
```

```powershell
# Windows
$pw = (Select-String '^SA_PASSWORD=' ../docker/.env).Line.Split('=', 2)[1]
dotnet ef dbcontext scaffold "Server=localhost,1433;Database=AdventureWorks2022;User Id=sa;Password=$pw;TrustServerCertificate=True" Microsoft.EntityFrameworkCore.SqlServer --output-dir Model --context AdventureWorksContext --no-onconfiguring --force --schema Person --schema HumanResources --schema Production --schema Purchasing --schema Sales --schema dbo
```

`--no-onconfiguring` keeps the connection string out of the code, and the `--schema` list keeps the T-SQL lessons' `lessonNN` tables out of the model. E01 explains what the scaffolder skipped.

## Troubleshooting

| Symptom | Fix |
|---|---|
| Every test fails with "Cannot connect to SQL Server on localhost,1433" | Start the container: `docker compose -f docker/docker-compose.yml up -d` (from the repo root) |
| "SA password not found" | Set `SA_PASSWORD`, or create `docker/.env` from `docker/.env.example`. A fresh git worktree doesn't have the gitignored `docker/.env`, so copy it over. |
| `A compatible .NET SDK was not found` | Install the .NET 10 SDK |
| Nothing runs, build errors in `Exercises.cs` | A half-written exercise doesn't compile. Fix it or comment it out. |
| Checks fail on row values that look right | Your AdventureWorks data may have drifted. `scripts/*/reset-db` restores a clean copy (and wipes the T-SQL lessons' `lessonNN` schemas). |
