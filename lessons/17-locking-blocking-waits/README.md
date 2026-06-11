# Lesson 17 — Locking, Blocking, Deadlocks & Waits

> **Prerequisite:** Tiers 1–4, especially lesson 11 (transactions & isolation). This lesson shows the *internals and diagnosis* of the concurrency behaviour introduced there.

## What you'll learn
- Lock modes (S, X, U, IS, IX), granularity, and lock escalation
- How to read a blocking chain (who is blocking whom, and on what)
- Deadlocks: how they form, the victim, and reading the deadlock graph
- The waits-and-queues methodology — the master lens for "what is the server waiting on?"

## Setup
Run `setup.sql` once. It creates the `lesson17` schema with a small `Account` table (blocking/deadlock demos) and a larger `BigOrders` table (lock-escalation demo). Re-run to reset.

**Several demos need two sessions.** Open a second terminal and run `.\scripts\connect.ps1` again — the lesson labels code blocks `-- Session A` and `-- Session B`.

## Concepts

### Lock modes & granularity
Locks protect data during transactions. Common modes: **S** (shared, reads), **X** (exclusive, writes), **U** (update, the intermediate step), and intent locks **IS/IX** at coarser levels. Locks are taken at row (KEY/RID), page, or object (table) granularity.

### Lock escalation
When a single statement holds too many fine-grained locks (~5000), SQL Server **escalates** them to one coarse lock (usually a table X lock) to save memory — which can suddenly block everyone else.

### Blocking
**Blocking** is normal, temporary contention: session B waits for a lock session A holds. `sys.dm_exec_requests.blocking_session_id` and `sys.dm_os_waiting_tasks` reveal the chain; `sys.dm_tran_locks` shows the exact locks.

### Deadlocks
A **deadlock** is a *cycle*: A holds a lock B wants, while B holds a lock A wants. SQL Server detects the cycle and kills one transaction (the **victim**, error 1205). Deadlock graphs are captured automatically in the `system_health` Extended Events session.

### Waits & queues
Every time a task can't proceed it records a **wait**. `sys.dm_os_wait_stats` (cumulative) and `sys.dm_os_waiting_tasks` (live) tell you what the server spends its time waiting on — the fastest path to a root cause.

## Worked Investigations (lesson17 schema)
1. Blocking chain via `sys.dm_exec_requests`.
2. Held/requested locks via `sys.dm_tran_locks`.
3. Live waiting tasks.
4. Lock escalation to an OBJECT lock.
5. Deadlock graph from `system_health`.
6. Top cumulative waits (the triage query).
7. Resetting wait stats to measure a specific workload.

## Common issues this explains
- A query "hanging" (it's blocked, not broken).
- Intermittent deadlock errors (1205) under concurrency.
- A bulk update suddenly blocking everyone (lock escalation).

## Pitfalls
- `NOLOCK` "fixes" blocking by reading dirty/duplicated/missing rows — almost never correct.
- `sys.dm_os_wait_stats` is cumulative since restart; reset it (dev) or snapshot-diff it to study one workload.
- Filter idle/benign waits (SLEEP_TASK, LAZYWRITER_SLEEP, etc.) or they drown the signal.
- Deadlock graphs age out of the `system_health` ring buffer — capture promptly.

## Cheatsheet link
See `cheatsheets/06-execution-plans.md` (and lesson 11 for isolation levels)

## Exercises
Open `exercises.sql` and work them in order (some need two sessions). Solutions in `exercises-solutions.sql`.
