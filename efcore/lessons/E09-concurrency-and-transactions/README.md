# E09 — Concurrency & Transactions

## What you'll learn

- Optimistic concurrency: concurrency tokens, the legacy `ModifiedDate` style vs `rowversion`, and `DbUpdateConcurrencyException`
- Resolving conflicts: store wins, client wins, and a field-by-field merge; avoiding lost updates
- Transactions in EF: `SaveChanges`' own transaction, its automatic savepoint inside yours, savepoints you create, and why there's no nesting
- Isolation levels in practice on a `READ_COMMITTED_SNAPSHOT` database: who waits for whom
- Blocking and lock timeouts, seen from EF and from `sys.dm_exec_requests`
- Why a retrying execution strategy and your own transactions don't mix

## Builds on

- T-SQL [lesson 11](../../../lessons/11-transactions-errors-concurrency/lesson.ipynb) — transactions, savepoints, isolation levels
- T-SQL [lesson 17](../../../lessons/17-locking-blocking-waits/lesson.ipynb) — locks, blocking, waits, `sys.dm_exec_requests`
- EF lessons [E07](../E07-change-tracking/README.md) (original vs current values) and [E08](../E08-savechanges-and-set-based-writes/README.md) (what `SaveChanges` sends)

## Setup

```
dotnet test --filter E09Examples --logger "console;verbosity=detailed"
```

`E09Context.cs` makes `Product.ModifiedDate` a concurrency token and maps a `rowversion` column, `Version`, on `ProductCategory` as a shadow property. AdventureWorks has no rowversion columns, so `E09Context.AddVersionColumnAsync(db)` adds one with `ALTER TABLE` **inside the test's transaction**. DDL is transactional in SQL Server, so the rollback removes it again. `RetryingContext` turns on `EnableRetryOnFailure`.

Some examples and checks open a second, independent session with `NewSessionAsync(isolationLevel)` (its own connection and transaction, also rolled back). Every lab connection runs with `SET LOCK_TIMEOUT 3000`, so a blocked statement fails after 3 seconds instead of waiting forever. Expect a few deliberate 3-second waits in this lesson.

## Concepts

### 1. Optimistic concurrency and tokens

EF assumes conflicts are rare and checks for them only when it writes: a property marked `IsConcurrencyToken()` (or `[ConcurrencyCheck]`) puts its **original** value into the UPDATE's or DELETE's WHERE clause. If someone changed the row since you read it, the statement matches 0 rows, and EF throws `DbUpdateConcurrencyException`. Nothing is locked while the user edits.

| Token | Who changes it | Risk |
|---|---|---|
| a normal column such as `ModifiedDate` (`IsConcurrencyToken()`) | your code, on every write | a writer that forgets to change it (a script, an `ExecuteUpdate`, another app) overwrites silently |
| `rowversion` (`IsRowVersion()`, a `byte[]`) | the database, on every write, automatically | none of the above. EF reads the new value back with `OUTPUT INSERTED.[Version]` |

In a web app the token has to survive the round trip to the browser. Render the version with the form, and on post set it as the property's **`OriginalValue`** before saving.

Examples: `ModifiedDate_as_a_concurrency_token`, `Rowversion_is_checked_and_refreshed`.

### 2. Resolving a conflict

`DbUpdateConcurrencyException.Entries` gives you the conflicting entries, and each has three sets of values: `OriginalValues` (what you read), `CurrentValues` (what you want) and `await entry.GetDatabaseValuesAsync()` (what's there now).

| Strategy | Code | Result |
|---|---|---|
| Store wins | `await entry.ReloadAsync()` | your edit is discarded |
| Client wins | `entry.OriginalValues.SetValues(databaseValues)`, save again | their edit is overwritten |
| Merge | per property: keep yours if `IsModified`, else take theirs; refresh all originals; save again | both survive |

For read-modify-write updates (stock levels, counters), "client wins" is a **lost update**: it writes a total computed from stale data. Reload, re-apply your change, and retry. Or let the database do the arithmetic in one statement (`ExecuteUpdate(... x.Stock + delta)`).

Example: `Store_wins_or_client_wins`.

### 3. Transactions and savepoints

- `SaveChanges` always runs in a transaction. With none open, it starts and commits its own, so a batch is all-or-nothing.
- With one already open (`Database.BeginTransaction()`, or `UseTransaction(...)` as this lab does), `SaveChanges` sets a **savepoint** first and rolls back to it if anything fails. The whole batch is undone, including statements that had succeeded, and the outer transaction stays usable. The failed entities are still Added or Modified in the tracker until you reload, detach or fix them.
- `Database.CurrentTransaction.CreateSavepointAsync(name)` / `RollbackToSavepointAsync(name)` set your own savepoints. Rolling back changes the database, not the tracked entities.
- There is no nesting. `BeginTransaction` while a transaction is open throws "The connection is already in a transaction". SQL Server's nested `BEGIN TRAN` only increments a counter, so EF doesn't pretend.

Examples: `A_failed_SaveChanges_rolls_back_its_whole_batch`, `Savepoints_you_create`, `BeginTransaction_inside_a_transaction_throws`.

### 4. Isolation levels on this database

AdventureWorks here has `READ_COMMITTED_SNAPSHOT` ON (and `ALLOW_SNAPSHOT_ISOLATION` OFF). Under the default `READ COMMITTED`, a reader therefore doesn't wait for a writer: it gets the last committed version of the row from the version store.

| Session isolation | Reading a row another transaction has changed but not committed |
|---|---|
| `READ COMMITTED` (with RCSI) | returns the last committed value, no waiting |
| `READ UNCOMMITTED` | returns the uncommitted value: a dirty read |
| `REPEATABLE READ` / `SERIALIZABLE` | waits for the lock (here: until the 3 s timeout) |

Pass the level to `BeginTransaction(IsolationLevel...)`, or here `NewSessionAsync(IsolationLevel...)`.

Example: `Readers_and_isolation_levels`.

### 5. Blocking and lock timeouts

Writers still block writers. An UPDATE of a row another transaction has updated waits for its exclusive lock. With `LOCK_TIMEOUT` set, SQL Server gives up with **error 1222** ("Lock request time out period exceeded"). EF classifies 1222 as transient and throws an `InvalidOperationException` ("likely due to a transient failure") with the `SqlException` as `InnerException`, so `catch (SqlException)` misses it. While it waits, the blocked request is visible in `sys.dm_exec_requests` with `blocking_session_id` and a `LCK_M_*` `wait_type`.

Example: `Writers_block_writers_until_the_lock_timeout`.

### 6. Execution strategies vs your transactions

`UseSqlServer(..., o => o.EnableRetryOnFailure())` retries operations that fail with transient errors, but it can only retry what it controls. Inside a transaction you opened, it can't replay the earlier statements, so it refuses: "The configured execution strategy ... does not support user-initiated transactions". The fix is to run the whole unit through the strategy: `db.Database.CreateExecutionStrategy().ExecuteAsync(async () => { using var tx = ...; ...; await tx.CommitAsync(); })`. (This lab can't show that fix: every test already runs inside the transaction that guarantees the rollback.)

Example: `Retrying_strategy_rejects_user_transactions`.

## Pitfalls

- **A `ModifiedDate` token that some writers don't update.** Their changes go unnoticed. Prefer `rowversion` where you can add one.
- **Setting a disconnected token as the current value.** For a token EF writes, the WHERE still compares the original. Set `OriginalValue`.
- **"Client wins" for increments.** Lost updates. Reload and re-apply, or do the arithmetic in SQL.
- **Two changes in one `SaveChanges` when the first must survive.** A failure undoes the whole batch.
- **`catch (SqlException)` around EF calls.** EF often wraps it. Check `InnerException`.
- **Assuming readers block on this database.** With RCSI they don't, but writers do.

## Exercises

```
dotnet test --filter E09Exercises
```

Edit `Exercises.cs`. *Skipped* = not attempted, *failed* = wrong, *passed* = done. Solutions in `Solutions.cs`.
