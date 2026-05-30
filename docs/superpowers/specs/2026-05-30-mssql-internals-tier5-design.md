# MSSQL Internals & Diagnostics (Tier 5) — Design Spec

**Date:** 2026-05-30
**Author:** brainstorming session with Claude (superpowers:brainstorming)
**Status:** Draft, awaiting user approval
**Builds on:** `2026-05-29-mssql-learning-design.md` (the original 13-lesson curriculum)

## Goal

Extend the existing MSSQL self-study curriculum with a fifth tier that takes the learner **under the hood**: how the engine actually works, so they can reason about *why* problems happen and recognize recurring issues and patterns. The learner has completed Tiers 1–4 (T-SQL fundamentals through indexes and execution plans) and now wants accurate mental models of the engine plus hands-on diagnostic skill.

The tier is **layered**: mental models first, diagnostic skills woven into each lesson, and a capstone that integrates everything into a symptom → root cause → fix workflow.

## Non-Goals

- Everything already out of scope in the base curriculum: HA / replication / Always-On, CLR / Service Broker, cloud-specific tooling, application-side data access, production hardening.
- Anything requiring more than the single Developer-edition container (a second *session* against the same container is fine; a second *instance* is not).
- Internals trivia for its own sake. Every concept must pay off in explaining an observable behavior or issue.
- Performance-counter dashboards / external monitoring tools. Diagnosis happens through built-in DMVs, DBCC, and execution plans.

## User Decisions (captured during brainstorm)

| Decision | Choice |
|---|---|
| Primary outcome | All three, layered: mental models + diagnostic skills + pattern catalog |
| Structure | Tier 5 lessons in the existing 5-file format (no new cheatsheet, no separate lab harness) |
| Engine areas | All four: storage & access, query lifecycle & optimizer, memory/buffer pool/log, locking/blocking/waits |
| Issues & patterns | Both: per-lesson "common issues" sections **and** a dedicated capstone troubleshooting lesson |
| Granularity | Approach A — 5 lessons (one per area + capstone) |

## Tier Structure & Placement

**New tier:** `Tier 5 — Under the Hood (Engine Internals & Diagnostics)`, lessons 14–18, appended after the existing 13. Same `lessons/NN-name/` convention and the five standard files per lesson. The capstone (18) builds on 14–17.

**Prerequisite framing:** Tier 5 assumes Tiers 1–4 are complete — especially lesson 12 (indexes) and lesson 13 (execution plans), which it deepens rather than repeats. Each Tier 5 `README.md` states this up front.

### Lesson map

| # | Directory | Mental model | Issues it explains |
|---|---|---|---|
| 14 | `14-storage-internals` | 8KB pages, extents, heaps vs B-trees, how a row is located, IAM | page splits, fragmentation, forwarded records in heaps |
| 15 | `15-query-lifecycle-optimizer` | parse → bind → optimize → execute; statistics & cardinality estimation; plan choice | stale stats, bad row estimates, plan-quality surprises |
| 16 | `16-memory-bufferpool-log` | buffer pool / data cache, checkpoint & lazy writer, PLE, write-ahead logging, the log & VLFs, recovery | low PLE / memory pressure, runaway log growth, tempdb spills |
| 17 | `17-locking-blocking-waits` | lock modes/granularity/escalation, blocking chains, deadlock graphs, waits-and-queues methodology | blocking, deadlocks, lock escalation |
| 18 | `18-diagnosing-common-issues` *(capstone)* | integrated root-cause workflow over a deliberately misbehaving workload | parameter sniffing, implicit conversion, key lookups, blocking, tempdb spill, stale stats — as one catalog |

### Updates to existing repo files

- Root `README.md` — add the Tier 5 row to the curriculum table.
- `CLAUDE.md` — add Tier 5 to the curriculum map and extend the authoring rules (see below).
- Root `README.md` — add a short "opening a second session" note (run `.\scripts\connect.ps1` in a second terminal) for the two-session demos.
- This spec is the companion design doc; the original 13-lesson spec is left unchanged.

## Per-Lesson Breakdown

### Lesson 14 — Storage & Access Internals

- **Concepts:** 8KB page anatomy (header, slot array, rows), extents (mixed vs uniform), heaps vs clustered B-trees, root → intermediate → leaf navigation, IAM pages, how seek vs scan maps to physical reads.
- **Demos (examples.sql):** `DBCC IND` / `sys.dm_db_database_page_allocations` to list pages; `DBCC TRACEON(3604)` + `DBCC PAGE` to crack open a real page; `sys.dm_db_index_physical_stats` for fragmentation and page density on a copied AdventureWorks table; deliberately trigger a page split and observe it.
- **Common issues this explains:** page splits from poor fill factor, fragmentation, heap forwarded records.
- **Exercises (investigate/diagnose):** find which page a given row lives on and prove it; cause and then measure a page split; diagnose a fragmented index and prescribe the fix.

### Lesson 15 — Query Lifecycle & the Optimizer

- **Concepts:** the four phases (parse, bind/algebrize, optimize, execute); what statistics are and how histograms drive cardinality estimates; trivial vs full optimization; why estimates diverge from actuals.
- **Demos:** `DBCC SHOW_STATISTICS` to read a histogram; estimated vs actual rows side-by-side; `sys.dm_exec_cached_plans` / `sys.dm_exec_query_stats` to inspect cached plans and reuse; force stale stats, watch the estimate go wrong, then `UPDATE STATISTICS` to fix.
- **Common issues this explains:** stale statistics, skewed data / bad estimates, the cardinality-estimation root of bad plans.
- **Exercises:** read a histogram and predict the estimate; find the cached plan for a query and report its reuse count; make a query mis-estimate, explain why, then fix it.

### Lesson 16 — Memory, Buffer Pool & the Log

- **Concepts:** the buffer pool as the data cache, clean vs dirty pages, checkpoint & lazy writer, Page Life Expectancy, write-ahead logging (WAL), the transaction log & VLFs, recovery (analysis/redo/undo).
- **Demos:** `sys.dm_os_buffer_descriptors` to show what's cached per database; PLE via `sys.dm_os_performance_counters`; `sys.dm_db_log_info` for VLFs; a long-running open transaction that prevents log truncation, shown via `log_reuse_wait_desc`; a memory-grant spill to tempdb visible in the actual plan.
- **Common issues this explains:** low PLE / memory pressure, runaway log growth from open transactions, tempdb spills.
- **Exercises:** show how much of a table is in cache before vs after a scan; create a condition that blocks log truncation and identify it; produce a tempdb spill and read it from the plan.

### Lesson 17 — Locking, Blocking, Deadlocks & Waits

- **Concepts:** lock modes (S/X/U/IS/IX), granularity & escalation, blocking chains, deadlock detection & the victim, the waits-and-queues methodology as the master diagnostic lens.
- **Demos (two sessions via a second `connect.ps1`):** live blocking inspected with `sys.dm_tran_locks`, `sys.dm_os_waiting_tasks`, `sys.dm_exec_requests`; force a deadlock and read the deadlock graph (from `system_health` Extended Events); `sys.dm_os_wait_stats` top-waits triage; lock escalation on a large update. Two-session blocks are deterministic (explicit `BEGIN TRAN`, ordered statements) and labeled `-- Session A` / `-- Session B`.
- **Common issues this explains:** blocking, deadlocks, lock-escalation surprises.
- **Exercises:** from session B, identify who is blocking session A and on what resource; trigger and explain a deadlock; interpret a top-waits snapshot and name the likely bottleneck.

### Lesson 18 — Capstone: Diagnosing Common Issues

- **Structure:** `setup.sql` seeds a deliberately misbehaving set of tables/procs. `examples.sql` walks one full diagnosis end-to-end as a worked model. Each pattern is documented as **symptom / behind-the-scenes cause / how to detect / how to fix**.
- **Pattern catalog:** parameter sniffing, implicit-conversion scan, key-lookup blowup, missing vs over-indexing, blocking, tempdb spill, stale stats.
- **Exercises:** a small "misbehaving workload" — each exercise hands the learner a symptom and asks them to find the root cause using the Tier 5 toolkit and apply a fix, with the reasoning (not just the fix) captured in `exercises-solutions.sql`.

## Data Strategy & Container Mechanics

### Per-lesson schema & data

- Each lesson owns a dedicated schema (`lesson14`…`lesson18`), drop-and-recreated in an idempotent `setup.sql` — identical to the existing convention.
- Internals lessons copy AdventureWorks tables (as lessons 12–13 already do) so they can be freely fragmented, bloated, reindexed, and locked without touching the pristine AdventureWorks tables.
- Lessons 16 and 18 need tables large enough to make memory/spill behavior visible; `setup.sql` inflates a copied table by self-inserting (e.g., cross-join multiply) to a few hundred MB rather than depending on a larger download. This stays within a single container's default memory.

### Container & DMV mechanics (verified against the single Developer container)

- `DBCC PAGE`, `DBCC IND`, and trace flag 3604 (`DBCC TRACEON(3604)`) work inside the container under SA — used in lesson 14.
- All DMVs referenced are server-scoped and available in Developer edition. The SA login already holds `VIEW SERVER STATE`, so no extra grants are needed.
- **Two-session demos (lessons 17 & 18):** the learner opens a second terminal and runs `.\scripts\connect.ps1` again for a second `sqlcmd` session. READMEs label blocks `-- Session A` / `-- Session B`. No new script is required; the root `README.md` gains a short "opening a second session" note.
- Deadlock/blocking demos are deterministic (ordered statements with explicit `BEGIN TRAN` and pauses) so they reproduce reliably rather than racing.

### Authoring rules (extend the existing set in CLAUDE.md)

- `setup.sql` idempotent, schema-scoped drop/recreate — unchanged rule.
- Where a demo's numbers are environment-dependent (cache warmth, container memory), the README explicitly says so and points the learner at the *operator / wait / direction*, not absolute values.
- Every exercise ships a solution in `exercises-solutions.sql`; for diagnose-style exercises the "solution" is the diagnostic query/steps **plus** a 2–4 line explanation of the root cause and fix.
- No exercise relies on a prior exercise's mutations; `setup.sql` re-seeds; `reset-db.ps1` remains the escape hatch.

## Per-Lesson Layout (format adaptation)

No structural change to the five-file format — only emphasis shifts for internals content:

- **`README.md`** — same skeleton; "Worked examples" become "Worked investigations," and a "Common issues this explains" subsection sits before Pitfalls. States the Tier 1–4 prerequisite.
- **`setup.sql`** — heavier here (copies/inflates tables, seeds bad scenarios) but the same idempotent, schema-scoped contract.
- **`examples.sql`** — runnable investigations/demos top-to-bottom, including two-session blocks marked `-- Session A/B`.
- **`exercises.sql`** — diagnose/investigate prompts with blank space below.
- **`exercises-solutions.sql`** — diagnostic steps **plus** a root-cause-and-fix explanation.

## Success Criteria

A learner finishing Tier 5 can:

1. Explain how a row is physically stored and located, and why an index seek beats a scan in page terms.
2. Describe the query lifecycle and read a statistics histogram to predict/explain a cardinality estimate.
3. Reason about the buffer pool, PLE, WAL, and the transaction log, and identify why the log won't truncate.
4. Diagnose live blocking and a deadlock from a second session, and triage with waits.
5. Take a misbehaving workload and drive symptom → root cause → fix using DMVs and execution plans.

## Open Questions / Risks

- **Environment-dependent numbers** — cache warmth and container memory make some metrics (PLE, fragmentation magnitude) non-deterministic. Mitigation: focus on operators/waits/direction; READMEs say so explicitly.
- **`DBCC PAGE` is semi-documented** — stable for decades and fine for learning, but the lesson notes it is a learning tool, not a production API.
- **Table-inflation time** — seeding a few-hundred-MB table in `setup.sql` may take a minute or two on first run; READMEs set that expectation.
- **SSMS vs sqlcmd for deadlock/plan graphs** — the graphical deadlock and plan views are richer in SSMS; lessons give the sqlcmd / `system_health` Extended Events path and note where SSMS shows it graphically.

## Out of Scope (Restated)

Anything that requires a second instance, additional services, external monitoring tooling, or features beyond the single Developer-edition container.
