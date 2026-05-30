# Lesson 16 — Memory, Buffer Pool & the Log

> **Prerequisite:** Tiers 1–4. Pairs with lesson 13 (plans) for the spill discussion.

## What you'll learn
- The buffer pool (data cache): how data lives in memory, clean vs dirty pages
- Checkpoint and the lazy writer; Page Life Expectancy as a memory-pressure signal
- Write-ahead logging (WAL), the transaction log, and Virtual Log Files (VLFs)
- Why the log won't truncate (`log_reuse_wait_desc`)
- Memory grants and tempdb spills

## Setup
Run `setup.sql` once. It creates the `lesson16` schema with `BigDetail` — an inflated copy of `SalesOrderDetail` large enough to make caching and spills visible. **First run may take a minute or two** to build the table. Re-run to reset.

## Concepts

### The buffer pool
SQL Server reads/writes 8KB pages in memory, not on disk directly. The **buffer pool** is that data cache. A page modified in memory is **dirty** until a **checkpoint** (or the **lazy writer** under memory pressure) flushes it to disk. `sys.dm_os_buffer_descriptors` shows what is cached.

### Page Life Expectancy (PLE)
The expected seconds a page stays cached before eviction. A healthy busy server keeps PLE high and stable; a sharp drop means SQL Server is churning the cache (memory pressure). Read it from `sys.dm_os_performance_counters`. On an idle dev box a low PLE can be meaningless — watch the *trend*.

### Write-ahead logging & the log
Before a data page change is written, the change is written to the **transaction log** (WAL). The log is divided into **VLFs** (`sys.dm_db_log_info`). The log can only be truncated (reused) once a portion is no longer needed; `sys.databases.log_reuse_wait_desc` tells you what it's waiting on — most commonly `ACTIVE_TRANSACTION` (an open transaction) or a pending backup in FULL recovery.

### Memory grants & spills
Sorts and hash joins request a **memory grant** before executing, sized from cardinality estimates. If the grant is too small (bad estimate, or forced), the operator **spills to tempdb** — extra slow I/O flagged on the plan operator. `sys.dm_exec_query_memory_grants` shows grants.

## Worked Investigations (lesson16 schema)
1. Cached pages per database.
2. A table loading into cache before/after a scan.
3. Page Life Expectancy.
4. VLF count.
5. `log_reuse_wait_desc`.
6. An open transaction holding the log (ACTIVE_TRANSACTION).
7. A forced tempdb spill + memory-grant DMV.

## Common issues this explains
- Low PLE / memory pressure on busy systems.
- A transaction log that grows uncontrollably because of a long-open transaction.
- tempdb spills slowing big sorts/joins.

## Pitfalls
- `DBCC DROPCLEANBUFFERS` / `CHECKPOINT` are dev-only diagnostics — never run them to "fix" production.
- Many tiny VLFs (log autogrown in small increments) slow recovery and log operations.
- A single forgotten open transaction can stop log truncation for the whole database.
- Memory-grant numbers vary by server memory and DOP — compare direction, not absolute KB.

## Cheatsheet link
See `cheatsheets/05-execution-plans.md`

## Exercises
Open `exercises.sql` and work them in order. Solutions in `exercises-solutions.sql`.
