# Notebook Format Rollout (Lessons 01–12, 14–18) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert the remaining 17 lessons to the 4-file notebook convention validated by the lesson 13 pilot, then retire the transitional wording in the docs.

**Architecture:** Every conversion task applies the normative recipe in `docs/superpowers/plans/2026-07-21-notebook-rollout-recipe.md` (rules R1–R12) to one lesson directory: merge README + examples into `lesson.ipynb`, split exercises into prompt/solution notebooks, verify all three via the sqlcmd harness, commit notebooks + deletions as one commit. Lesson-specific notes below capture known hazards from prior work; everything else is the recipe.

**Tech Stack:** nbformat 4 JSON, Azure Data Studio SQL kernel, MSSQL 2022 in Docker (`mssql-learn`), PowerShell/Bash harness.

## Global Constraints

- The recipe file is normative for every task; its R2 skeleton, R3 verbatim-transfer rules, R9 harness (with `-I`), and R11 commit format are binding.
- `setup.sql` files are never edited.
- `lessons/13-execution-plans-and-tuning/` is already converted — never touched by this plan.
- One lesson per task, one commit per lesson, sequential execution (no parallel implementers).
- R10 (minimal pre-existing-bug fixes, documented) applies; a fix that changes teaching intent is BLOCKED, not improvised.

---

### Task 1: Convert lesson 01 (setup-and-basics)
**Files:** `lessons/01-setup-and-basics/` per recipe R1.
**Notes:** Setup-heavy README (tool installation, connection walkthrough) — that prose stays markdown-only; only actual T-SQL becomes code cells. Known ledger note: an ORDER-BY-in-views bullet has imprecise wording — transfer verbatim anyway (content fixes are out of scope).
- [ ] Apply recipe R1–R12; verify; commit `feat: convert lesson 01 to notebook format`

### Task 2: Convert lesson 02 (tsql-core-select)
**Files:** `lessons/02-tsql-core-select/` per recipe R1.
- [ ] Apply recipe R1–R12; verify; commit `feat: convert lesson 02 to notebook format`

### Task 3: Convert lesson 03 (data-types-and-expressions)
**Files:** `lessons/03-data-types-and-expressions/` per recipe R1.
**Notes:** KNOWN PRE-EXISTING BUG (tracked since prior plan): `examples.sql` Example 4 fails with Msg 245 (`VarcharCode='A001'` compared to int). Apply R10: minimal fix (make the comparison type-consistent while keeping the example's implicit-conversion teaching point), document in report.
- [ ] Apply recipe R1–R12 (+R10 fix); verify; commit `feat: convert lesson 03 to notebook format`

### Task 4: Convert lesson 04 (joins-and-sets)
**Files:** `lessons/04-joins-and-sets/` per recipe R1.
- [ ] Apply recipe R1–R12; verify; commit `feat: convert lesson 04 to notebook format`

### Task 5: Convert lesson 05 (aggregations-and-grouping)
**Files:** `lessons/05-aggregations-and-grouping/` per recipe R1.
- [ ] Apply recipe R1–R12; verify; commit `feat: convert lesson 05 to notebook format`

### Task 6: Convert lesson 06 (subqueries-and-ctes)
**Files:** `lessons/06-subqueries-and-ctes/` per recipe R1.
- [ ] Apply recipe R1–R12; verify; commit `feat: convert lesson 06 to notebook format`

### Task 7: Convert lesson 07 (window-functions)
**Files:** `lessons/07-window-functions/` per recipe R1.
- [ ] Apply recipe R1–R12; verify; commit `feat: convert lesson 07 to notebook format`

### Task 8: Convert lesson 08 (dml-insert-update-delete-merge)
**Files:** `lessons/08-dml-insert-update-delete-merge/` per recipe R1.
**Notes:** DML examples mutate lesson08 tables; harness runs after a setup reset so single-pass must succeed. Watch batching around any triggers/OUTPUT demos (R7).
- [ ] Apply recipe R1–R12; verify; commit `feat: convert lesson 08 to notebook format`

### Task 9: Convert lesson 09 (ddl-and-constraints)
**Files:** `lessons/09-ddl-and-constraints/` per recipe R1.
**Notes:** KNOWN: computed-column example previously failed under plain sqlcmd (Msg 1934) — the harness's `-I` resolves it; no source fix expected. Exercises create objects without idempotency guards — fine single-pass after reset; do not add guards (out of scope).
- [ ] Apply recipe R1–R12; verify; commit `feat: convert lesson 09 to notebook format`

### Task 10: Convert lesson 10 (views-procedures-functions)
**Files:** `lessons/10-views-procedures-functions/` per recipe R1.
**Notes:** KNOWN: source files are missing `GO` before several `CREATE OR ALTER` statements (Msg 111 under sqlcmd). R7 cell-splitting fixes this structurally — each module-creating statement gets its own cell. Verify every `CREATE OR ALTER` cell contains nothing else.
- [ ] Apply recipe R1–R12; verify; commit `feat: convert lesson 10 to notebook format`

### Task 11: Convert lesson 11 (transactions-errors-concurrency)
**Files:** `lessons/11-transactions-errors-concurrency/` per recipe R1.
**Notes:** Contains two-session demos (RCSI, deadlock). Apply R8's `-- [manual]` marker to blocks the source designates for a second session or that would block/deadlock in a single sequential run; everything else must pass the harness. Document every `-- [manual]` cell in the report.
- [ ] Apply recipe R1–R12 (+R8 manual cells); verify; commit `feat: convert lesson 11 to notebook format`

### Task 12: Convert lesson 12 (indexes)
**Files:** `lessons/12-indexes/` per recipe R1.
**Notes:** KNOWN: filtered-index Example 5 previously failed under plain sqlcmd (Msg 1934) — harness `-I` resolves it; no source fix expected.
- [ ] Apply recipe R1–R12; verify; commit `feat: convert lesson 12 to notebook format`

### Task 13: Convert lesson 14 (storage-internals)
**Files:** `lessons/14-storage-internals/` per recipe R1.
**Notes:** Uses DBCC PAGE/IND-style inspection (often with TF 3604) — these run fine in code cells; R8's commented-out rule applies to anything the source keeps commented.
- [ ] Apply recipe R1–R12; verify; commit `feat: convert lesson 14 to notebook format`

### Task 14: Convert lesson 15 (query-lifecycle-optimizer)
**Files:** `lessons/15-query-lifecycle-optimizer/` per recipe R1.
- [ ] Apply recipe R1–R12; verify; commit `feat: convert lesson 15 to notebook format`

### Task 15: Convert lesson 16 (memory-bufferpool-log)
**Files:** `lessons/16-memory-bufferpool-log/` per recipe R1.
**Notes:** R8: cache-flush commands (`DBCC DROPCLEANBUFFERS` etc.) stay runnable if the source runs them uncommented (intentional on the dev container); markdown-fence only what the source keeps commented.
- [ ] Apply recipe R1–R12; verify; commit `feat: convert lesson 16 to notebook format`

### Task 16: Convert lesson 17 (locking-blocking-waits)
**Files:** `lessons/17-locking-blocking-waits/` per recipe R1.
**Notes:** Heaviest two-session lesson (blocking chains, deadlocks, lock escalation). Same R8 `-- [manual]` policy as Task 11; single-session-safe content must still pass the harness. Document every manual cell.
- [ ] Apply recipe R1–R12 (+R8 manual cells); verify; commit `feat: convert lesson 17 to notebook format`

### Task 17: Convert lesson 18 (complex-queries, capstone)
**Files:** `lessons/18-complex-queries/` per recipe R1.
- [ ] Apply recipe R1–R12; verify; commit `feat: convert lesson 18 to notebook format`

### Task 18: Docs sweep — retire transitional wording
**Files:** Modify `CLAUDE.md`, `README.md`. Check-only: `cheatsheets/*.md`, `docs/superpowers/specs/*.md`.
- [ ] In `CLAUDE.md`: replace the converted/unconverted dual-convention bullet with a single statement — every lesson directory contains exactly four files (`setup.sql`, `lesson.ipynb`, `exercises.ipynb`, `exercises-solutions.ipynb`); drop "Converted so far: lesson 13" and the old five-file description entirely.
- [ ] In `README.md`: collapse the "Most lesson directories… / Lessons converted…" split into one sentence describing the notebook layout for all lessons (keep the ADS how-to-run paragraph).
- [ ] Grep the repo (excluding `docs/superpowers/plans/`, `docs/superpowers/specs/`, `.superpowers/`) for references to per-lesson `README.md`, `examples.sql`, `exercises.sql`, `exercises-solutions.sql`; fix stale learner-facing references (cheatsheets etc.) to point at the notebooks. Historical specs/plans stay untouched.
- [ ] Commit `docs: single notebook convention after full lesson rollout`

---

After Task 18: final whole-branch review, then a user spot-check gate in ADS (open 2–3 converted lessons across tiers, run cells, confirm rendering) mirroring the pilot's Task 6.
