# E12 — Migrations Workflow

## What you'll learn

- The migrations workflow: change the model, `migrations add`, **read what was scaffolded**, apply
- What a migration consists of (`Up`, `Down`, the Designer file, the model snapshot) and how `__EFMigrationsHistory` tracks a database
- The data-loss traps: renames scaffolded as drop + add, defaults for new required columns, splitting a column, and how to edit a migration to keep the data
- Custom SQL in migrations, and why it needs `EXEC` to survive an idempotent script
- `HasData` vs `UseSeeding` for data
- Applying migrations: idempotent scripts and bundles at deploy time vs `Migrate()` at run time

## Builds on

- T-SQL [lesson 09](../../../lessons/09-ddl-and-constraints/lesson.ipynb) — `ALTER TABLE`, constraints, and what schema changes do to existing rows
- EF lesson [E10](../E10-fluent-api-modeling/README.md) — the model that migrations turn into schema

## Setup

```
dotnet tool restore                       # once: the pinned dotnet-ef 10.0.12
dotnet test --filter E12Examples --logger "console;verbosity=detailed"
```

**E12 works differently from the other lessons.** Your model lives in `Exercises.cs`: `Customer` and `E12ExerciseContext`, in namespace `EfCoreLessons.E12.Exercise`. Each exercise asks you to:
1. change that model
2. create a migration from `efcore/`:

   ```
   dotnet ef migrations add <Name> --context E12ExerciseContext --output-dir lessons/E12-migrations-workflow/ExerciseMigrations
   ```

3. read and, where needed, edit the generated migration in `ExerciseMigrations/`
4. return the migration's name from the exercise method

The checks apply your migrations to a fresh **`EfLab_E12`** database, one step at a time, inserting rows in between to see whether the data survives. Migrations commit for real, so E12 uses that disposable database (dropped and recreated per test) instead of a rolled-back transaction. AdventureWorks is never involved.

Made a mistake? `dotnet ef migrations remove --context E12ExerciseContext --force` deletes the last migration and restores the snapshot. `--force` is needed because the checks leave `EfLab_E12` at the last migration they applied, and plain `remove` refuses to delete an applied migration; the lab database doesn't matter, the next test recreates it. `--force` runs the migration's Down on EfLab_E12 first. If that fails, run `dotnet ef database drop --context E12ExerciseContext --force` (it targets EfLab_E12), then a plain `remove`. To fix only a Down, just edit the migration file. Keep your model compiling between steps: the whole test project (every lesson) builds together. `dotnet ef migrations list --context E12ExerciseContext` shows the chain.

The solution's migrations are in `SolutionMigrations/`. They were generated exactly like this, then edited where the Why notes say.

## Concepts

### 1. The workflow and the files

`migrations add` compares your current model with the **model snapshot** (the model as of the last migration) and scaffolds the operations in between:

| File | Contents |
|---|---|
| `<timestamp>_<Name>.cs` | `Up` (apply) and `Down` (revert): `CreateTable`, `AddColumn`, `RenameColumn`, `Sql(...)` and so on |
| `<timestamp>_<Name>.Designer.cs` | the full model at that migration |
| `<Context>ModelSnapshot.cs` | the model now, which the next `migrations add` diffs against |

Commit all three. The timestamp orders migrations, and the class name must be unique per context.

### 2. Applying, tracking, reverting

`__EFMigrationsHistory` lists the ids of the migrations a database has applied. `Migrate()` (or `dotnet ef database update`) applies the pending ones in order. `MigrateAsync("<earlier id>")` runs `Down` methods back to that point, and `"0"` reverts them all. `GetAppliedMigrations()` and `GetPendingMigrations()` compare the assembly with the history table.

Examples: `The_history_table_records_applied_migrations`, `Migrating_down_runs_Down`.

### 3. Scaffolding is a draft: the data-loss traps

EF scaffolds a migration from the *schema* difference. It can't know what you meant to do with the data:

| Model change | Scaffolded | What happens to the data | Fix in the migration |
|---|---|---|---|
| rename a property, nothing else | `RenameColumn` | kept | none |
| rename **and** change it (here: widen) | `DropColumn` + `AddColumn`, "may result in the loss of data" | **deleted** | `RenameColumn` + `AlterColumn` |
| add a required (`NOT NULL`) column | `AddColumn(..., defaultValue: <CLR default>)` | existing rows get `false` / `0` / `""` | choose the default, or backfill with `Sql(...)` |
| replace one column with two | `DropColumn` first | **deleted** before anything can copy it | add, copy with `Sql(...)`, then drop |

Write `Down` with the same care: it runs when someone rolls back. Each operation also maps to provider SQL; a rename is `EXEC sp_rename`.

Example: `Operations_become_SQL`.

### 4. Scripts, custom SQL and deploying

Applying migrations in production is usually a deploy step, not app code:

- **`dotnet ef migrations script --idempotent`** (`GenerateScript(options: Idempotent)`) wraps each migration in `IF NOT EXISTS (... __EFMigrationsHistory ...)`, so it runs safely against a database at any state, and a DBA can review it. `--from`/`--to` (`GenerateScript(from, to)`) covers a range.
- **`dotnet ef migrations bundle`** builds a self-contained executable that applies the pending migrations, so the deploy server needs no SDK.
- **`Migrate()` at startup** is simplest. EF 9+ takes a database-wide lock while migrating, but running schema changes from every app instance, with app permissions, is still risky. Prefer checking `GetPendingMigrations()` at startup and failing fast.

The idempotent script has a catch. SQL Server **compiles a whole batch before running any of it**, including statements inside an `IF` that will be skipped. Custom SQL that names a column created (or dropped) by the same migration fails to compile in the script, even though `Migrate()`, which runs statement by statement, works fine. Wrap such SQL in `EXEC(N'...')` so it compiles only when it runs.

Examples: `Idempotent_script_guards_every_migration`, `Script_for_a_range_of_migrations`.

### 5. Data: `HasData` vs `UseSeeding`

| | `HasData` | `UseSeeding` / `UseAsyncSeeding` (EF 9+) |
|---|---|---|
| Where | model configuration | context options |
| Becomes | `InsertData`/`UpdateData` in migrations | code that runs after every `Migrate`/`EnsureCreated` |
| Good for | fixed reference rows with known keys | "insert if missing", computed or environment-specific data |
| Must be | stable (keys given explicitly) | idempotent (it runs on every start) |

### 6. `EnsureCreated` is not a migration

`EnsureCreated()` creates the current schema directly, with no history table. That's fine for tests and prototypes (E10–E11 use the same machinery), but a database created that way can't be migrated later: `Migrate()` tries to create tables that already exist. Pick one per database.

Example: `EnsureCreated_and_migrations_dont_mix`.

## Pitfalls

- **Applying a scaffolded migration without reading it.** The "may result in the loss of data" warning is easy to scroll past.
- **Editing the model snapshot or Designer files by hand.** Change the model and re-scaffold (or `migrations remove`) instead.
- **Removing or editing a migration a shared database already applied.** Add a new migration instead.
- **Custom SQL without `EXEC` in an idempotent script.** It works locally and fails on deploy.
- **Calling `Migrate()` from every web server at startup.** Use a deploy step (script or bundle).
- **Mixing `EnsureCreated` and migrations on one database.**

## Exercises

```
dotnet test --filter E12Exercises
```

Do them in order: each builds on the model and migrations of the one before. *Skipped* = not attempted, *failed* = wrong, *passed* = done. Solutions in `Solutions.cs` and `SolutionMigrations/`.
