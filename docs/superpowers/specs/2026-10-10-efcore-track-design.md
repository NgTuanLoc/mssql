# EF Core Practice Track — Design Spec

**Date:** 2026-10-10
**Author:** brainstorming session with Claude (superpowers:brainstorming)
**Status:** Approved in conversation, awaiting written-spec review

## Goal

A practice track for **EF Core 10** aimed at a learner who has already used EF Core and wants
depth: predicting the SQL EF generates, diagnosing loading and performance problems,
controlling change tracking, handling concurrency, and designing and migrating code-first
models. It runs against this repo's existing Docker MSSQL 2022 + AdventureWorks2022 setup.
Exercises are xUnit tests that verify **behavior** (round trips, SQL shape, tracker state,
exceptions, schema), not just result rows.

## Relationship to the T-SQL curriculum

- A separate track in `efcore/`. `lessons/` (01–18), `docker/`, `scripts/` and `cheatsheets/`
  are unchanged.
- The 2026-05-29 curriculum spec lists "application-side data access (ORMs, ...)" as a
  non-goal. That still applies to the T-SQL track; this spec defines the EF Core track's own
  scope.
- Each EF lesson links back to the T-SQL lesson(s) it builds on.

## User Decisions (captured during brainstorm)

| Decision | Choice |
|---|---|
| Starting level | Has used EF Core; wants depth (generated SQL, performance, change tracking, concurrency) |
| Scope | All four areas: querying & SQL translation, loading & performance, change tracking & concurrency, modeling & migrations |
| Format | xUnit exercises whose checks assert behavior, not just results |
| Runtime | EF Core 10.0.12 on .NET 10 (both LTS, supported to Nov 2028) |
| Database | Existing `mssql-learn` container: AdventureWorks2022 for database-first lessons, `EfLab_*` databases for code-first lessons |

### Alternatives considered

- **.NET 10 file-based apps** (`dotnet run exercises.cs`): closest to the notebook feel, but
  no automatic checking, and `dotnet ef` migrations need a real project anyway.
- **Notebooks:** Polyglot Notebooks / .NET Interactive was deprecated by Microsoft on
  2026-03-27, so a C# notebook track has no supported runtime.

## Non-Goals

- EF Core basics from zero — the track assumes prior use.
- ASP.NET Core, DI hosting, repository/unit-of-work patterns — contexts are created directly.
- Providers other than SQL Server.
- EF Core 11 features — the track pins EF Core 10.
- Third-party EF extensions and bulk-copy libraries.

## Repository Layout

```
efcore/
  README.md                  track overview, setup, commands, lesson table
  EfCoreLessons.csproj       the only project; everything below compiles into it
  global.json                pins SDK 10.0.x (this machine's default SDK is an 11 preview)
  xunit.runner.json          disables parallel test execution
  dotnet-tools.json          pins dotnet-ef 10.0.12 as a local tool (the .NET 10 SDK's default location)
  Lab/                       shared test harness
  Model/                     scaffolded AdventureWorksContext + entities (generated, committed)
  lessons/
    E01-setup-and-generated-sql/
      README.md
      Examples.cs
      Checks.cs
      Exercises.cs
      Solutions.cs
    E02-…/ … E13-…/
```

No solution file: `dotnet test` and IDEs work from the `.csproj`. Each lesson's namespace is
`EfCoreLessons.E<NN>` and its classes carry the lesson ID (`E05Examples`, `E05Checks`,
`E05Exercises`, `E05Solutions`) so test filters are unambiguous.

## Databases

### AdventureWorks2022 — database-first lessons (E01–E09, E13)

- Every test runs on its own connection and transaction, **rolled back** when the test ends.
  The EF track never modifies AdventureWorks, needs no `setup.sql`, and tests are
  order-independent and re-runnable.
- Extra contexts in a test share that connection and transaction, so optimistic-concurrency
  conflicts and disconnected-graph scenarios work without committing anything.
- Scenarios that need a truly independent session (blocking and lock timeouts in E09) open a
  second connection inside the test; it rolls back too.

### `EfLab_*` — code-first lessons (E10–E12)

- Databases named `EfLab_<lesson>` (e.g. `EfLab_E10`), created on first use by the tests
  themselves. E10–E11 create each test's tables inside the test's rolled-back transaction
  (`IRelationalDatabaseCreator.CreateTables`), so the database stays empty between tests; E12
  drops and recreates `EfLab_E12` per test, because migrations commit. They need no reset
  script; stale ones are harmless.
- This deliberately departs from the `lessonNN`-schema convention: `EnsureCreated` does
  nothing on a database that already has tables, and `EnsureDeleted` /
  `dotnet ef database drop` would drop all of AdventureWorks.
- **Guard:** the harness refuses to create, drop or migrate any database whose name does not
  start with `EfLab_`.

### Connection and execution

- `localhost,1433`, user `sa`, password from the `SA_PASSWORD` environment variable, falling
  back to `docker/.env` (found by walking up from the test output directory), matching the
  existing helper scripts. `TrustServerCertificate=True` for the container's self-signed
  certificate. No credentials are committed anywhere, including `Model/`.
- Tests run serially (`parallelizeTestCollections: false`): SQL capture stays deterministic,
  blocking demos can't interfere with each other, and a lesson's Exercises and Solutions
  classes can share its `EfLab_` database.

## Model (scaffolded)

- Generated once from AdventureWorks2022 and committed:

  ```
  dotnet ef dbcontext scaffold "<connection string>" Microsoft.EntityFrameworkCore.SqlServer \
    --output-dir Model --context AdventureWorksContext --no-onconfiguring \
    --schema Person --schema HumanResources --schema Production \
    --schema Purchasing --schema Sales --schema dbo
  ```

  `--no-onconfiguring` keeps the connection string out of the code. The explicit `--schema`
  list keeps any `lessonNN` tables created by the T-SQL track out of the model. Configuration
  stays in the Fluent API (the default), which E10 builds on.
- The scaffolder skips `HumanResources.Employee.OrganizationNode` (hierarchyid) and
  `Person.Address.SpatialLocation` (geography), and drops the `Production.Document` and
  `Production.ProductDocument` tables entirely (their primary key is a hierarchyid), because the
  HierarchyId and NetTopologySuite plugin packages are not added — no lesson needs them, and E01
  explains the skip. It does declare every trigger with `HasTrigger` (10 triggers on 8 tables).
- **`Model/` is never hand-edited.** Shared corrections go in a partial-class file outside
  `Model/` (implementing the scaffolded `OnModelCreatingPartial`). Lesson-specific model
  changes go in a context derived from `AdventureWorksContext` inside the lesson folder; EF
  caches one model per context type, so no lesson can change another lesson's model.

## Lesson Anatomy

| File | Role |
|---|---|
| `README.md` | What you'll learn, builds-on links to T-SQL lessons and cheatsheets, concepts (referring to named examples), pitfalls, run commands |
| `Examples.cs` | Worked examples as passing tests; each prints the SQL it ran and a result summary to test output. Examples that demonstrate an error assert it (e.g. `Assert.Throws`) so they still pass |
| `Checks.cs` | Abstract `E<NN>Checks : LabTest`: one abstract method and one `[Fact]` per exercise/challenge, plus the result types (records) each exercise returns — the EF equivalent of "Expected columns" |
| `Exercises.cs` | `E<NN>Exercises : E<NN>Checks`: prompts as comments; every override is `throw new NotImplementedException()`. The only file the learner edits |
| `Solutions.cs` | `E<NN>Solutions : E<NN>Checks`: identical prompts, worked answers, a `// Why:` note after each explaining the approach and, where relevant, the tempting-but-wrong alternative |

Sketch:

```csharp
// Checks.cs
public abstract class E05Checks : LabTest
{
    protected abstract Task<List<OrderSummary>> Ex1(AdventureWorksContext db);

    [Fact] public async Task Ex1_OrdersWithCustomer()
    {
        var result = await Ex1(Db);

        Assert.Equal(await Reference.Rows<OrderSummary>("SELECT ..."), result);
        Sql.AssertRoundTrips(1);                  // N+1 fails here
        Assert.Empty(Db.ChangeTracker.Entries()); // must be untracked
    }
}

// Exercises.cs
public class E05Exercises : E05Checks
{
    // Exercise 1: <prompt — what to return, the rules the check enforces, optional hint>
    protected override Task<List<OrderSummary>> Ex1(AdventureWorksContext db)
        => throw new NotImplementedException();
}
```

- xUnit runs the inherited facts once per derived class, so one set of assertions serves
  both the learner's attempts and the solutions — the two can't drift apart.
- Lessons that need model changes add a sixth file, `E<NN>Context.cs`: a context derived from
  `AdventureWorksContext` (see Model). Their checks derive from `LabTest<E<NN>Context>`.
- Code-first lessons (Tier D) add `E<NN>Model.cs`: the plain classes the exercises map. E12
  also has `SolutionMigrations/` (committed) and the learner's `ExerciseMigrations/`.
- Each lesson has 5 exercises (`Ex1`–`Ex5`) and 3–4 challenges (`Ch1`–`Ch4`); challenges
  combine concepts and may draw on earlier lessons.
- An exercise that still throws `NotImplementedException` is reported **skipped**; a wrong
  answer **fails**; a correct one **passes**. Checks use an `[Exercise]` attribute (a `[Fact]`
  whose `SkipExceptions` contains `NotImplementedException`), so no check needs extra code for it.

### What checks assert

- Result rows, compared against a reference T-SQL query run through `Reference` where
  practical (robust to data drift), otherwise against values verified on a fresh restore.
- Round-trip count.
- Generated SQL shape where it is the lesson's point (e.g. `EXISTS` rather than `COUNT`,
  only the needed columns selected).
- Change-tracker state, expected exceptions, and — for code-first lessons — the resulting
  schema.

Checks test outcomes rather than dictating a single implementation, unless the SQL shape is
what the exercise teaches.

## Lab Harness

`LabTest<TContext>` (a fresh instance per test, as xUnit creates one per test; `LabTest` is
`LabTest<AdventureWorksContext>`) provides:

- `Db` — a `TContext` on the test's connection and transaction, rolled back on dispose.
- `NewContext()` — another `TContext` on the same connection and transaction.
- `Sql` — every command run by `Db` and `NewContext()` contexts, captured by a
  `DbCommandInterceptor` and printed to test output. `Sql.AssertRoundTrips(n)` lists the
  captured commands in its failure message.
- `Reference` — runs the checks' own reference queries on the same transaction without being
  counted in `Sql`.
- `QueryPlan.OperatorsAsync(db, query)` (added with Tier B) — the estimated plan's physical
  operators via `SET SHOWPLAN_XML`, without executing the query or counting it in `Sql`.
- `NewContext<T>(params IInterceptor[])` and `NewSessionAsync(isolationLevel)` (added with Tier C) — a
  context of another type or with extra interceptors on the test's transaction; and a second,
  independent session with its own rolled-back transaction. Every lab connection runs
  `SET LOCK_TIMEOUT 3000`, and disposal attempts every step even if one fails, so a failing test
  can't leave a transaction (and its locks) open. Commits are refused (`CommitGuard`, and
  `TransactionGuard` for raw `COMMIT`), so nothing a test does can become permanent.

`LabDatabase` (added with Tier D) creates and drops `EfLab_<lesson>` databases and enforces the
`EfLab_` guard (letters, digits and `_` only, checked before any SQL). `CodeFirstTest` gives
code-first tests a rolled-back transaction in their lesson's database, and builds each model
from a configuration method with a model-cache key of (method, declaring type), so the
learner's model (Exercises) and the solution's never share EF's model cache.

**E12 (migrations):** separate exercise and solution contexts, each with its own migrations
folder and design-time factory. The learner runs `dotnet ef migrations add` against the
exercise context; checks apply the migrations to a fresh `EfLab_` database and assert schema
and data survival (e.g. rows seeded before a column rename must still be there after it).

## Curriculum

13 lessons in four tiers plus a capstone, ~2–4 hours each (~30–45 hours total).

### Tier A — Querying & SQL translation (AdventureWorks, database-first)

| # | Lesson | Depth focus | Builds on |
|---|---|---|---|
| E01 | Setup & seeing the SQL | How `Model/` was scaffolded and what scaffolding skips; `LogTo`, `ToQueryString`, `TagWith`; basic LINQ → WHERE / ORDER BY / OFFSET-FETCH; parameters vs inlined constants; where client evaluation throws | 01–02 |
| E02 | Projections, joins & nulls | DTO projections; navigation joins vs `Join`; EF 10 `LeftJoin`/`RightJoin` vs the GroupJoin/`DefaultIfEmpty` pattern; set operations; C# null vs SQL NULL (null compensation) | 03–04 |
| E03 | Grouping, subqueries & where LINQ runs out | GroupBy translation limits; `Any`/`All`/`Contains` → EXISTS/IN; parameterized collections; top-N-per-group (EF emits ROW_NUMBER); what has no LINQ form (window functions, ROLLUP, recursive CTEs) | 05–07 |
| E04 | Raw SQL, views, functions & procedures | `FromSql` vs `FromSqlRaw` (injection); composing LINQ over raw SQL; `SqlQuery<T>`; keyless entities over views; mapping TVFs and scalar UDFs; non-composable stored procedures | 10, 18 |

### Tier B — Loading & performance

| # | Lesson | Depth focus | Builds on |
|---|---|---|---|
| E05 | Loading related data | Lazy vs explicit vs eager loading; filtered Include; projection vs Include; N+1; cartesian explosion; `AsSplitQuery` and its consistency trade-off | 04, 13 |
| E06 | Query performance | `AsNoTracking` vs `AsNoTrackingWithIdentityResolution`; compiled queries; keyset vs offset paging; streaming vs buffering; SARGable LINQ (`.Year ==` vs a date range); plan-cache pollution; finding EF queries in the plan-cache DMVs via `TagWith` (Query Store is off in the container, and enabling it would change the database); `HasIndex` | 12–13, 15 |

### Tier C — Change tracking & concurrency

| # | Lesson | Depth focus | Builds on |
|---|---|---|---|
| E07 | Change tracking internals | Entity states; snapshot `DetectChanges`; `ChangeTracker.DebugView`; identity resolution; original vs current values; disconnected `Attach`/`Update`; graph pitfalls | — (EF-only) |
| E08 | SaveChanges & set-based writes | The SQL SaveChanges emits (batching, MERGE/OUTPUT); AdventureWorks triggers vs the OUTPUT clause (`HasTrigger`); `ExecuteUpdate`/`ExecuteDelete` vs load-modify-save; cascade delete client vs DB; a `SaveChangesInterceptor` that stamps `ModifiedDate` | 08 |
| E09 | Concurrency & transactions | Concurrency tokens (`ModifiedDate` as a legacy token, rowversion); resolving `DbUpdateConcurrencyException`; explicit transactions and savepoints; isolation levels; blocking and lock timeouts; retrying execution strategies vs user transactions | 11, 17 |

### Tier D — Modeling & migrations (code-first, `EfLab_*` databases)

| # | Lesson | Depth focus | Builds on |
|---|---|---|---|
| E10 | Modeling with the Fluent API | Keys; relationships and delete behaviors; owned vs complex types; value converters; shadow properties and backing fields; named query filters | 09 |
| E11 | Inheritance & JSON columns | TPH vs TPT vs TPC — the schema and SQL each produces and its performance; `ToJson` columns (nvarchar on SQL Server 2022) and querying into them | 09 |
| E12 | Migrations workflow | Reading generated migrations and the model snapshot; the rename-vs-drop+add data-loss trap; custom SQL in migrations; `HasData` vs `UseSeeding`; idempotent scripts and bundles; applying at deploy time vs runtime | — |

### Capstone

| # | Lesson | Depth focus | Builds on |
|---|---|---|---|
| E13 | Capstone | Port lesson-18-style reports to LINQ under round-trip and no-client-evaluation limits, falling back to raw SQL where LINQ can't express them; a transactional order placement with conflict handling; model customization in a derived context (e.g. a keyless view mapping) | 18 |

## Tooling & Versions

- Target `net10.0`. Packages: `Microsoft.EntityFrameworkCore.SqlServer` and `.Design` at
  **10.0.12** (`.Proxies` 10.0.12 joins in phase 2 for E05's lazy-loading demo); `xunit.v3.mtp-off`
  4.0.2, `xunit.runner.visualstudio` 4.0.1 and `Microsoft.NET.Test.Sdk` 18.10.1. xUnit v3 4.x
  defaults to Microsoft.Testing.Platform; the `mtp-off` package keeps `dotnet test` on VSTest so
  `--filter` uses the syntax shown below.
- All versions are exact — no floating ranges.
- `global.json` pins SDK 10.0.x with `rollForward: latestFeature` (never an 11 preview).
- `dotnet-tools.json` pins `dotnet-ef` 10.0.12; `dotnet tool restore` once. The machine's
  global `dotnet-ef` 9.0.7 is left alone.
- The xUnit analyzer rule xUnit1051 (pass `TestContext.Current.CancellationToken` to every async
  call) is suppressed: lessons favor readable queries over cancellation plumbing.
- EF Core 11 (GA November 2026) is out of scope. Upgrading later = bump versions, re-run the
  solutions.
- `.gitignore` gains `bin/`, `obj/`, `TestResults/`.

## Commands

| Purpose | Command (run in `efcore/`) |
|---|---|
| First run | `dotnet tool restore`, then `dotnet test --filter E01Examples` |
| Practice loop | `dotnet test --filter E05Exercises` |
| See a lesson's examples with their SQL | `dotnet test --filter E05Examples --logger "console;verbosity=detailed"` |
| Self-check the whole track | `dotnet test --filter "FullyQualifiedName!~Exercises"` |

## Documentation Updates

- `efcore/README.md` (new): prerequisites (.NET 10 SDK; container running with AdventureWorks
  restored per the root README), the commands above, the lesson table, the scaffold command.
- Root `README.md`: a short "EF Core track" section linking to `efcore/README.md`.
- `CLAUDE.md`: an "EF Core track" section with this spec's conventions — layout, five-file
  lesson anatomy, Checks/Exercises/Solutions mechanics, the rollback rule, `EfLab_` databases
  and guard, `Model/` is generated, verification commands.

## Verification (definition of done, per lesson)

1. `dotnet build` produces no new warnings.
2. `dotnet test --filter "FullyQualifiedName~E<NN>&FullyQualifiedName!~Exercises"` — all
   examples and solutions pass against a freshly restored AdventureWorks2022.
3. `dotnet test --filter E<NN>Exercises` — every check is skipped; none fail.
4. Prompts in `Exercises.cs` and `Solutions.cs` are identical.
5. Every EF Core 10-specific claim in the README and `Why:` notes is checked against
   Microsoft Learn (behaviors that changed between versions — parameterized collection
   translation, `LeftJoin`/`RightJoin`, named query filters, complex types — especially).

## Build-out Phases

Each phase gets its own implementation plan in `docs/superpowers/plans/`:

1. **Foundation + Tier A** — project, tooling pins, `Lab` harness, scaffolded `Model/`, docs,
   E01–E04.
2. **Tier B** — E05–E06.
3. **Tier C** — E07–E09.
4. **Tier D** — E10–E12, adding `LabDatabase`.
5. **Capstone** — E13.

## Risks

Verified while planning phase 1 (2026-10-10, against the live container):

- **Scaffold fidelity — resolved.** The EF 10 scaffolder emits `HasTrigger` for all 10
  triggers; the model builds with 0 warnings; the only losses are the hierarchyid/geography
  columns and the two hierarchyid-keyed tables listed under Model.
- **"Not attempted = skipped" — resolved.** `FactAttribute.SkipExceptions` (xUnit v3 4.0.2)
  reports unimplemented exercises as skipped.
- **`Reference` isolation — resolved.** A second context on the same connection and
  transaction, without the capture interceptor.

Still open:

- **Run time:** serial execution; after Tier D, 324 tests run in ~30 s (E09's deliberate lock
  timeouts and E12's database recreation account for most of it). Revisit if a full run gets
  slow.

## Out of Scope (Restated)

Changes to the T-SQL lessons, Docker setup or helper scripts; providers other than SQL
Server; EF Core 11; hosting/DI frameworks; notebook runtimes.
