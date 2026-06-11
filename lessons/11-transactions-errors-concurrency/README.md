# Lesson 11 — Transactions, Errors & Concurrency

## What you'll learn
- `BEGIN TRANSACTION` / `COMMIT` / `ROLLBACK`
- `TRY / CATCH` and `THROW` for error handling
- `ERROR_MESSAGE()`, `ERROR_NUMBER()`, `ERROR_LINE()`, `ERROR_SEVERITY()`
- Isolation levels: Read Committed, RCSI, Repeatable Read, Snapshot, Serializable
- Deadlock demo (two-session walkthrough)

## Setup
Run `setup.sql` once. It creates the `lesson11` schema with an `Account` table (three rows). Re-run `setup.sql` to reset balances between exercise attempts.

## Concepts

### Transaction boundaries

```sql
BEGIN TRANSACTION;
  -- one or more DML statements
COMMIT TRANSACTION;   -- makes changes permanent
-- or
ROLLBACK TRANSACTION; -- undoes all changes since BEGIN TRANSACTION
```

`@@TRANCOUNT` tracks nesting level. Always check `IF @@TRANCOUNT > 0` before rolling back inside a CATCH block.

### TRY / CATCH

```sql
BEGIN TRY
    BEGIN TRANSACTION;
    -- risky statements
    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;   -- re-raise the original error to the caller
END CATCH;
```

`THROW` (SQL Server 2012+) re-raises the current error with its original number. `RAISERROR` is the older alternative.

### Custom errors with THROW

```sql
THROW 50001, 'Custom error message.', 1;
-- number must be >= 50000; severity 16 = non-fatal user error
```

### Isolation levels

| Level | Dirty reads | Non-repeatable reads | Phantom reads | Locking behaviour |
|---|---|---|---|---|
| Read Uncommitted | yes | yes | yes | No shared locks — fastest, least safe |
| **Read Committed** (default) | no | yes | yes | Shared lock released immediately after read |
| RCSI | no | yes | yes | Row versioning — readers don't block writers |
| Repeatable Read | no | no | yes | Shared locks held until end of transaction |
| Serializable | no | no | no | Range locks — safest, highest contention |
| Snapshot | no | no | no | Row versioning — fully non-blocking reads |

Set per-session: `SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;`

**RCSI** (Read Committed Snapshot Isolation) is highly recommended for OLTP workloads. Enable with:
```sql
ALTER DATABASE AdventureWorks2022 SET READ_COMMITTED_SNAPSHOT ON;
```

### Deadlock demo (two SSMS sessions)

```sql
-- Session 1                          -- Session 2
BEGIN TRAN;                            BEGIN TRAN;
UPDATE lesson11.Account                UPDATE lesson11.Account
  SET Balance = 1 WHERE AccountID=1;    SET Balance = 2 WHERE AccountID=2;
-- wait...                             -- wait...
UPDATE lesson11.Account                UPDATE lesson11.Account
  SET Balance = 2 WHERE AccountID=2;    SET Balance = 1 WHERE AccountID=1;
-- One session becomes the deadlock victim (error 1205) and rolls back.
```

SQL Server detects the cycle and kills one session (the "deadlock victim"). The victim receives error 1205. The other session's transaction completes.

## Worked Examples (lesson11 schema)
1. Basic transfer: BEGIN/COMMIT.
2. Rollback on constraint violation.
3. TRY/CATCH with THROW — propagate error to caller.
4. CATCH error functions: `ERROR_MESSAGE()`, `ERROR_SEVERITY()`, `ERROR_LINE()`.
5. Isolation level demo (two-session walkthrough — see README).
6. Check RCSI setting on AdventureWorks.
7. `sys.dm_exec_sessions` — inspect current session isolation level.

## Pitfalls
- Forgetting `IF @@TRANCOUNT > 0` before ROLLBACK in CATCH — causes error if no active transaction.
- `XACT_ABORT ON` auto-rolls back on any error (useful in procs); off by default.
- Nested transactions: `COMMIT` inside a nested transaction does not commit — only the outermost `COMMIT` does. `ROLLBACK` always rolls back to the outermost begin.
- `THROW` without arguments can only be used inside a CATCH block (re-raises current error). Outside a CATCH, use `THROW number, msg, state`.
- RCSI adds TempDB write overhead for row versions — acceptable for most OLTP, but monitor TempDB growth.

## Cheatsheet link
See `cheatsheets/01-tsql-syntax.md`

## Exercises
Open `exercises.sql` and try them in order. Re-run `setup.sql` to reset Account balances. Solutions in `exercises-solutions.sql`.
