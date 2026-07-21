# Notebook Lesson Format Pilot (Lesson 13) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert lesson 13 (execution-plans-and-tuning) from the 5-file convention to the 4-file notebook convention (`setup.sql` + `lesson.ipynb` + `exercises.ipynb` + `exercises-solutions.ipynb`), verified against the Docker AdventureWorks2022 instance, per spec `docs/superpowers/specs/2026-07-21-notebook-lesson-format-design.md`.

**Architecture:** Each `.ipynb` is a hand-written nbformat-4 JSON file targeting Azure Data Studio's native SQL kernel — markdown cells carry the current README/comment prose, code cells carry the current `.sql` statements. Content is a 1:1 transfer from the existing working-copy files (no rewording of taught content beyond format-required adjustments listed per task). Scripted verification extracts code-cell sources and pipes them through `sqlcmd` in the `mssql-learn` container; final graphical/ADS validation is a manual user gate.

**Tech Stack:** nbformat 4 JSON, Azure Data Studio SQL kernel, MSSQL 2022 in Docker (`mssql-learn` container), PowerShell for verification.

## Global Constraints

- `setup.sql` is NOT modified by this plan (spec: "No changes … to the `setup.sql` execution flow").
- No changes to `docker/`, `scripts/win/`, `scripts/mac/`.
- All notebooks committed with cleared outputs: every code cell has `"outputs": [], "execution_count": null`.
- No connection strings or credentials inside notebooks. `SA_PASSWORD` comes from `docker/.env` only in verification commands.
- Every notebook's first code cell starts with `USE AdventureWorks2022;`.
- Kernel metadata for every notebook (ADS SQL kernel), exactly:
  ```json
  "metadata": {
    "kernelspec": { "name": "SQL", "display_name": "SQL", "language": "sql" },
    "language_info": { "name": "sql", "version": "" }
  },
  "nbformat": 4,
  "nbformat_minor": 2
  ```
- Markdown/code cell `source` is written as a single JSON string (nbformat allows string or list; use string). Markdown cells: `{"cell_type": "markdown", "metadata": {}, "source": "..."}`. Code cells: `{"cell_type": "code", "metadata": {}, "source": "...", "outputs": [], "execution_count": null}`.
- Commit messages use conventional-commit prefixes (`feat:`, `fix:`, `docs:`); no attribution footer (disabled globally).
- The **working copy** of lesson 13 (including its uncommitted changes, committed in Task 1) is the source of truth for all content transfers. Line references below are to the working-copy versions.
- SQL content is transferred verbatim — do not rewrite queries, change values, or "improve" comments beyond the explicit transformations each task lists.

## Verification harness (used by Tasks 2–4)

There is no test suite in this repo; the verification cycle for each notebook is:

1. **JSON/nbformat validity:** parse with `ConvertFrom-Json` (fails loudly on malformed JSON).
2. **SQL executability:** extract all code-cell sources, join with `GO` batch separators, pipe through `sqlcmd -b` in the container (nonzero exit on any SQL error). Run lesson 13's `setup.sql` first so each verification starts from a reset schema.

The verification PowerShell (parameterize `$nbPath` per task) — run from the repo root `D:\Study\Database\mssql`:

```powershell
# 0. Ensure container is up and lesson schema is reset
docker compose -f docker/docker-compose.yml up -d
$sa = ((Get-Content docker/.env) | Where-Object { $_ -match '^SA_PASSWORD=' }) -replace '^SA_PASSWORD=',''
Get-Content lessons/13-execution-plans-and-tuning/setup.sql -Raw |
  docker exec -i mssql-learn /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$sa" -No -b

# 1. Parse notebook (JSON validity check)
$nb = Get-Content $nbPath -Raw | ConvertFrom-Json

# 2. Extract code cells, join with GO, execute
$sql = ($nb.cells | Where-Object { $_.cell_type -eq 'code' } |
        ForEach-Object { ($_.source -join '') + "`nGO" }) -join "`n"
$tmp = Join-Path $env:TEMP 'nb-verify.sql'
Set-Content -Path $tmp -Value $sql -Encoding UTF8
Get-Content $tmp -Raw |
  docker exec -i mssql-learn /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$sa" -No -b
if ($LASTEXITCODE -ne 0) { throw "notebook SQL failed" }
```

Expected on success: setup prints `Lesson 13 setup complete.`; the notebook run prints result rows plus `Table '...'. Scan count N, logical reads N, ...` statistics messages; final exit code 0.

Note: joining cells with `GO` mirrors how ADS executes each cell as its own batch. Cells that contain only comments produce empty batches, which is valid.

---

### Task 1: Commit in-flight lesson 13 WIP so conversion starts from a clean tree

Lesson 13 currently has uncommitted changes: `setup.sql` (+50 lines: `SkewedOrders` skewed table + supporting indexes for future challenge exercises), `examples.sql` (+2 `GO` separators before procedure batches), `exercises-solutions.sql` (+1 `GO`, +1 `DROP INDEX IF EXISTS` re-run guard). These are pre-existing enhancement work, not part of this conversion — commit them first so the conversion commits are clean.

**Files:**
- Commit (no edits): `lessons/13-execution-plans-and-tuning/setup.sql`, `lessons/13-execution-plans-and-tuning/examples.sql`, `lessons/13-execution-plans-and-tuning/exercises-solutions.sql`

**Interfaces:**
- Produces: a clean working tree for `lessons/13-execution-plans-and-tuning/`; the committed working-copy content is what Tasks 2–4 transfer into notebooks.

- [ ] **Step 1: Confirm the pending diff is only the three files described above**

Run: `git status --short lessons/13-execution-plans-and-tuning/`
Expected: exactly ` M examples.sql`, ` M exercises-solutions.sql`, ` M setup.sql` under that path. If anything else appears, stop and ask the user.

- [ ] **Step 2: Verify setup.sql still runs end-to-end (it is the one file this plan keeps)**

```powershell
docker compose -f docker/docker-compose.yml up -d
$sa = ((Get-Content docker/.env) | Where-Object { $_ -match '^SA_PASSWORD=' }) -replace '^SA_PASSWORD=',''
Get-Content lessons/13-execution-plans-and-tuning/setup.sql -Raw |
  docker exec -i mssql-learn /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$sa" -No -b
```

Expected: exit 0, last line `Lesson 13 setup complete.`

- [ ] **Step 3: Commit**

```bash
git add lessons/13-execution-plans-and-tuning/setup.sql \
        lessons/13-execution-plans-and-tuning/examples.sql \
        lessons/13-execution-plans-and-tuning/exercises-solutions.sql
git commit -m "fix: lesson 13 batch separators, re-run guard, and skewed-table setup for future challenges"
```

---

### Task 2: Create `lesson.ipynb` (README + examples merged)

**Files:**
- Create: `lessons/13-execution-plans-and-tuning/lesson.ipynb`
- Read (sources, unchanged): `lessons/13-execution-plans-and-tuning/README.md`, `lessons/13-execution-plans-and-tuning/examples.sql`

**Interfaces:**
- Consumes: clean working tree from Task 1.
- Produces: `lesson.ipynb` whose code cells, joined with `GO`, execute cleanly after `setup.sql`. Task 5 deletes its two source files.

- [ ] **Step 1: Write `lesson.ipynb` with exactly the following cell sequence**

Use the JSON skeleton from Global Constraints. Cells in order (MD = markdown cell, CODE = code cell). "Verbatim from X:N–M" means copy those working-copy lines exactly, dropping SQL `-- Example N:` / `-- Plan:` comment markers only where the table below says the prose moves to markdown.

| # | Type | Content |
|---|---|---|
| 1 | MD | Title + intro. Verbatim from `README.md:1–12` (title, "What you'll learn", "Setup"), then **replace** README line 14 (the SSMS-strongly-recommended paragraph) with this new paragraph: `**Tooling note:** run the SQL cells in this notebook with Azure Data Studio connected to the mssql-learn container (attach the SQL kernel to your saved localhost connection). For the graphical execution-plan steps, switch to SSMS and enable the actual plan (Ctrl+M) — its plan viewer shows operator costs, tooltips, and memory grant warnings that ADS does not expose as richly. SET STATISTICS IO/TIME output appears inline below each cell in ADS.` |
| 2 | CODE | `USE AdventureWorks2022;\nGO\nSET STATISTICS IO   ON;\nSET STATISTICS TIME ON;` |
| 3 | MD | `## Concepts` + "Getting plans" table + "Reading plans — key rules". Verbatim from `README.md:16–32`. |
| 4 | MD | Operator quick reference table. Verbatim from `README.md:34–43`. |
| 5 | MD | `## Worked examples\n\n### Example 1 — Index Seek\n\nSalesOrderID is the clustered key. Expect a Clustered Index Seek with very few logical reads.` |
| 6 | CODE | The Example 1 query, verbatim from `examples.sql:13–15` (SELECT … WHERE SalesOrderID = 43659;). |
| 7 | MD | `### Example 2 — Index Scan\n\nNo useful predicate, so every row is read. Expect a Clustered Index Scan that reads every page — compare the logical reads with Example 1.` |
| 8 | CODE | The Example 2 query, verbatim from `examples.sql:19`. |
| 9 | MD | `### Example 3 — Nonclustered seek, no key lookup\n\nThe filter on CustomerID uses IX_lesson13_SOH_CustomerID; its INCLUDE columns cover OrderDate and TotalDue, so there is no Key Lookup in the plan.` |
| 10 | CODE | The Example 3 query, verbatim from `examples.sql:23–25`. |
| 11 | MD | SARGability concept: verbatim from `README.md:45–56` (heading, definition, rewrite table in the ```sql fence), then append: `\n### Example 4 — SARGable vs non-SARGable\n\nRun the next two cells and compare the plan operator (Scan vs Seek) and logical reads. The function on the column in the first query defeats the index on OrderDate.` |
| 12 | CODE | Non-SARGable query, verbatim from `examples.sql:30–32` (WHERE YEAR(OrderDate) = 2014). |
| 13 | CODE | SARGable rewrite, verbatim from `examples.sql:35–37` (range predicate). |
| 14 | MD | `SET STATISTICS IO` output explanation. Verbatim from `README.md:58–64`. |
| 15 | MD | `### Example 5 — Hash Match join\n\nJoining two large row sets — expect a Hash Match (or Merge Join) rather than Nested Loops.` |
| 16 | CODE | The Example 5 join, verbatim from `examples.sql:41–44`. |
| 17 | MD | Parameter sniffing concept: verbatim from `README.md:66–80` (heading, explanation, the three-option ```sql fence), then append: `\n### Example 6 — parameter sniffing demo\n\nThe next cell creates the procedure; the cell after primes the plan cache with a small territory, then reuses that sniffed plan for a large one.` |
| 18 | CODE | `CREATE OR ALTER PROCEDURE lesson13.usp_OrdersByTerritory …` verbatim from `examples.sql:50–54` (no trailing GO — the cell is its own batch). |
| 19 | CODE | The two EXECs with their inline comments, verbatim from `examples.sql:57–58`, plus the trailing comment line `examples.sql:59`. |
| 20 | MD | `To see territory 1 get its own (better) plan, flush the plan cache and re-run — demo only, never in production:\n\n```sql\nDBCC FREEPROCCACHE;\nEXEC lesson13.usp_OrdersByTerritory @TerritoryID = 1;\n```\n\n### Example 7 — OPTIMIZE FOR UNKNOWN\n\nBreaks sniffing without paying RECOMPILE on every execution: the optimizer uses average statistics instead of the first sniffed value.` |
| 21 | CODE | `CREATE OR ALTER PROCEDURE lesson13.usp_OrdersByTerritory_Fixed …` verbatim from `examples.sql:67–72`. |
| 22 | CODE | `EXEC lesson13.usp_OrdersByTerritory_Fixed @TerritoryID = 1;` (from `examples.sql:75`). |
| 23 | CODE | `SET STATISTICS IO   OFF;\nSET STATISTICS TIME OFF;` |
| 24 | MD | Pitfalls: verbatim from `README.md:91–96`. Then cheatsheet + exercises pointer, replacing README lines 98–102 with: `## Cheatsheet link\nSee cheatsheets/06-execution-plans.md\n\n## Exercises\nOpen exercises.ipynb and work through it (use SSMS with Ctrl+M when a prompt asks about the plan). Solutions with explanations are in exercises-solutions.ipynb.` |

Note: README's "Worked Examples (lesson13 schema)" numbered list (lines 82–89) is intentionally dropped — it was a table of contents for `examples.sql`, and the examples are now inline.

- [ ] **Step 2: Verify — run the verification harness**

Run the harness from the header with `$nbPath = "lessons/13-execution-plans-and-tuning/lesson.ipynb"`.
Expected: exit 0; output includes result rows for examples 1–5, `Table 'SalesOrderHeader'. Scan count …` statistics lines, and no error messages. (The commented-out DBCC line lives in markdown, so nothing destructive runs.)

- [ ] **Step 3: Confirm outputs are empty in the committed file**

Run: `powershell -Command "($nb = Get-Content lessons/13-execution-plans-and-tuning/lesson.ipynb -Raw | ConvertFrom-Json).cells | Where-Object { $_.cell_type -eq 'code' -and $_.outputs.Count -ne 0 } | Measure-Object | Select-Object -ExpandProperty Count"`
Expected: `0`

- [ ] **Step 4: Commit**

```bash
git add lessons/13-execution-plans-and-tuning/lesson.ipynb
git commit -m "feat: lesson 13 notebook pilot - lesson.ipynb merging README and examples"
```

---

### Task 3: Create `exercises.ipynb` (prompts, no solutions)

**Files:**
- Create: `lessons/13-execution-plans-and-tuning/exercises.ipynb`
- Read (source, unchanged): `lessons/13-execution-plans-and-tuning/exercises.sql`

**Interfaces:**
- Consumes: clean tree from Task 1.
- Produces: `exercises.ipynb`; its prompt markdown cells are reused **verbatim, in the same order** by Task 4's solutions notebook.

- [ ] **Step 1: Write `exercises.ipynb` with exactly the following cell sequence**

Same JSON skeleton as Global Constraints. Given/starter queries are copied verbatim from `exercises.sql` (line refs below). Answer placeholders are code cells containing only the placeholder comment, or markdown cells where the expected answer is prose.

| # | Type | Content |
|---|---|---|
| 1 | MD | `# Lesson 13 — Exercises\n\nRun setup.sql first, and work through lesson.ipynb before attempting these. Use SSMS with the actual execution plan enabled (Ctrl+M) whenever a prompt asks about plan operators. Solutions with explanations: exercises-solutions.ipynb.` |
| 2 | CODE | `USE AdventureWorks2022;\nGO\nSET STATISTICS IO   ON;\nSET STATISTICS TIME ON;` |
| 3 | MD | `## Exercise 1 — SARGable rewrite\n\nRun the query below and look at its execution plan. Identify: (a) the plan operator on Sales.SalesOrderDetail, (b) estimated vs actual rows, (c) logical reads. Then rewrite the WHERE clause to be SARGable and compare.` |
| 4 | CODE | The given non-SARGable query, verbatim from `exercises.sql:12–16` (the CONVERT date filter, including its `-- non-SARGable` comment). |
| 5 | CODE | `-- Your SARGable rewrite here:` |
| 6 | MD | `## Exercise 2 — eliminate a key lookup\n\nThe query below triggers a key lookup. Add an index to lesson13.SalesOrderHeader to eliminate it, then verify with the execution plan that the key lookup is gone.` |
| 7 | CODE | The given query, verbatim from `exercises.sql:23–25` (SELECT … WHERE TerritoryID = 4). |
| 8 | CODE | `-- Your CREATE INDEX here:` |
| 9 | MD | `## Exercise 3 — selectivity and access path\n\nRun both queries and compare their plans and logical reads. Explain why the plans differ.` |
| 10 | CODE | Both queries, verbatim from `exercises.sql:32–34` (Status = 5 and Status = 1, with their `-- Query A/B` comments). |
| 11 | MD | `**Your explanation here:** _(edit this cell)_` |
| 12 | MD | `## Exercise 4 — parameter-sniffing-safe procedure\n\nCreate a stored procedure lesson13.usp_RevenueByTerritory that accepts @TerritoryID INT and returns SUM(TotalDue) grouped by YEAR(OrderDate). Call it with territory 1 (large) and territory 10 (small) and check whether the plan is appropriate for both. Add OPTION (OPTIMIZE FOR (@TerritoryID UNKNOWN)) if needed.` |
| 13 | CODE | `-- Your CREATE PROCEDURE and test EXECs here:` |
| 14 | MD | `## Exercise 5 — JOIN vs correlated subquery\n\nUse the SET STATISTICS IO output to compare logical reads for the two queries below. Which one is more efficient, and why?` |
| 15 | CODE | Query A (explicit JOIN), verbatim from `exercises.sql:48–53`. |
| 16 | CODE | Query B (correlated subquery), verbatim from `exercises.sql:55–59`. |
| 17 | MD | `**Your analysis here:** _(edit this cell)_` |
| 18 | CODE | `SET STATISTICS IO   OFF;\nSET STATISTICS TIME OFF;` |

- [ ] **Step 2: Verify — run the verification harness**

Run the harness from the header with `$nbPath = "lessons/13-execution-plans-and-tuning/exercises.ipynb"`.
Expected: exit 0. The given/starter queries all execute (comment-only answer cells produce empty batches, which is fine).

- [ ] **Step 3: Confirm outputs are empty**

Same check as Task 2 Step 3, against `exercises.ipynb`. Expected: `0`.

- [ ] **Step 4: Commit**

```bash
git add lessons/13-execution-plans-and-tuning/exercises.ipynb
git commit -m "feat: lesson 13 notebook pilot - exercises.ipynb"
```

---

### Task 4: Create `exercises-solutions.ipynb`

**Files:**
- Create: `lessons/13-execution-plans-and-tuning/exercises-solutions.ipynb`
- Read (sources, unchanged): `lessons/13-execution-plans-and-tuning/exercises-solutions.sql`, `lessons/13-execution-plans-and-tuning/exercises.ipynb` (for the shared prompt cells)

**Interfaces:**
- Consumes: the five `## Exercise N — …` markdown prompt cells from Task 3's `exercises.ipynb` — copy them **verbatim** so both notebooks show identical prompts.
- Produces: `exercises-solutions.ipynb`; Task 5 deletes `exercises-solutions.sql`.

- [ ] **Step 1: Write `exercises-solutions.ipynb` with exactly the following cell sequence**

Same JSON skeleton. Solution SQL is verbatim from the working-copy `exercises-solutions.sql`; the explanatory `--` comments above each solution become the rationale markdown cell that follows the solution cell.

| # | Type | Content |
|---|---|---|
| 1 | MD | `# Lesson 13 — Exercise Solutions\n\nWorked solutions with a short rationale after each. Run setup.sql first. Use SSMS (Ctrl+M) to see the plan effects each rationale describes.` |
| 2 | CODE | `USE AdventureWorks2022;\nGO\nSET STATISTICS IO   ON;\nSET STATISTICS TIME ON;` |
| 3 | MD | Exercise 1 prompt — verbatim copy of `exercises.ipynb` cell 3. |
| 4 | CODE | Solution query, verbatim from `exercises-solutions.sql:10–14` (the SARGable date-range version, including the `-- SARGable` comment). |
| 5 | MD | `**Why:** CONVERT() on the column is non-SARGable and forces a scan; a date range lets the index on OrderDate seek. Expected: fewer logical reads and an Index Seek instead of a Scan on SalesOrderHeader.` |
| 6 | MD | Exercise 2 prompt — verbatim copy of `exercises.ipynb` cell 6. |
| 7 | CODE | Solution, verbatim from `exercises-solutions.sql:18–25` (DROP INDEX IF EXISTS re-run guard, CREATE INDEX with INCLUDE, then the verification SELECT). |
| 8 | MD | `**Why:** keying on TerritoryID with INCLUDE (SalesPersonID, SubTotal) makes the index covering, so the Key Lookup operator disappears from the plan. The DROP INDEX IF EXISTS line is a re-run guard.` |
| 9 | MD | Exercise 3 prompt — verbatim copy of `exercises.ipynb` cell 9. |
| 10 | CODE | Both queries, verbatim from `exercises-solutions.sql:33–34`. |
| 11 | MD | `**Why:** Status = 5 (shipped) covers most rows — the optimizer may choose a Clustered Index Scan because seeking plus looking up each row would cost more than one full scan. Status = 1 (in process) covers very few rows, so it chooses an Index Seek. Selectivity drives the access-path choice.` |
| 12 | MD | Exercise 4 prompt — verbatim copy of `exercises.ipynb` cell 12. |
| 13 | CODE | The procedure, verbatim from `exercises-solutions.sql:38–48` (CREATE OR ALTER … OPTION (OPTIMIZE FOR (@TerritoryID UNKNOWN)) — no surrounding GO; the cell is its own batch). |
| 14 | CODE | The two test EXECs, verbatim from `exercises-solutions.sql:51–52`. |
| 15 | MD | `**Why:** OPTIMIZE FOR UNKNOWN compiles the plan from average statistics instead of the first sniffed @TerritoryID, so one cached plan is reasonable for both the large territory (1) and the small one (10).` |
| 16 | MD | Exercise 5 prompt — verbatim copy of `exercises.ipynb` cell 14. |
| 17 | CODE | Query A, verbatim from `exercises-solutions.sql:56–60`. |
| 18 | CODE | Query B, verbatim from `exercises-solutions.sql:65–68`. |
| 19 | MD | `**Why:** the JOIN makes one pass over SalesOrderDetail with a Hash Match aggregate. The correlated subquery aggregates SalesOrderDetail once per product — ~500 executions for ~500 products — so Query A has far fewer logical reads. Correlated aggregates over a large table are almost always better written as a JOIN with GROUP BY.` |
| 20 | CODE | `SET STATISTICS IO   OFF;\nSET STATISTICS TIME OFF;` |

- [ ] **Step 2: Verify — run the verification harness**

Run the harness from the header with `$nbPath = "lessons/13-execution-plans-and-tuning/exercises-solutions.ipynb"`.
Expected: exit 0; the CREATE INDEX and CREATE OR ALTER PROCEDURE cells succeed (setup.sql reset at the start of the harness plus the in-cell DROP INDEX guard make re-runs safe).

- [ ] **Step 3: Confirm outputs are empty**

Same check as Task 2 Step 3, against `exercises-solutions.ipynb`. Expected: `0`.

- [ ] **Step 4: Commit**

```bash
git add lessons/13-execution-plans-and-tuning/exercises-solutions.ipynb
git commit -m "feat: lesson 13 notebook pilot - exercises-solutions.ipynb"
```

---

### Task 5: Retire the old files and update `CLAUDE.md`

**Files:**
- Delete: `lessons/13-execution-plans-and-tuning/README.md`, `lessons/13-execution-plans-and-tuning/examples.sql`, `lessons/13-execution-plans-and-tuning/exercises.sql`, `lessons/13-execution-plans-and-tuning/exercises-solutions.sql`
- Modify: `CLAUDE.md:29–38` (Lesson conventions section) and `CLAUDE.md:9–27` (Running SQL section — add a Notebooks note)

**Interfaces:**
- Consumes: Tasks 2–4 committed (all content transferred — deletion is safe only after those commits exist).
- Produces: the final 4-file lesson 13 layout; updated repo conventions for future lesson conversions.

- [ ] **Step 1: Confirm every deleted file's content exists in a committed notebook**

Run: `git log --oneline -3`
Expected: the three `feat: lesson 13 notebook pilot …` commits from Tasks 2–4 are present. If any is missing, stop — do not delete sources.

- [ ] **Step 2: Delete the retired files**

```bash
git rm lessons/13-execution-plans-and-tuning/README.md \
       lessons/13-execution-plans-and-tuning/examples.sql \
       lessons/13-execution-plans-and-tuning/exercises.sql \
       lessons/13-execution-plans-and-tuning/exercises-solutions.sql
```

- [ ] **Step 3: Update `CLAUDE.md` lesson conventions**

Replace this bullet (currently `CLAUDE.md:33`):

```markdown
- Every lesson directory `lessons/NN-topic/` contains exactly five files: `README.md` (concepts + walkthrough), `setup.sql`, `examples.sql`, `exercises.sql`, `exercises-solutions.sql`.
```

with:

```markdown
- Lessons are migrating to a notebook format (design spec: `docs/superpowers/specs/2026-07-21-notebook-lesson-format-design.md`). A **converted** lesson directory contains exactly four files: `setup.sql`, `lesson.ipynb` (concepts + runnable examples, merged), `exercises.ipynb` (prompts only), `exercises-solutions.ipynb` (same prompts + worked solutions). An **unconverted** lesson still has the old five files: `README.md`, `setup.sql`, `examples.sql`, `exercises.sql`, `exercises-solutions.sql`. Converted so far: lesson 13.
- Notebooks target Azure Data Studio's native SQL kernel (`kernelspec` name `SQL`). First code cell is `USE AdventureWorks2022;`. Commit notebooks with all cell outputs cleared. Never embed connection details or credentials in a notebook.
```

- [ ] **Step 4: Add a Notebooks note to the Running SQL section**

Insert after the paragraph ending `…wiping all `lessonNN` schemas.` (currently `CLAUDE.md:27`):

```markdown
Notebook lessons (`.ipynb`) are run in Azure Data Studio: open the notebook, attach the SQL kernel to the saved `localhost` connection for the `mssql-learn` container, and run cells top to bottom. To verify a notebook's SQL non-interactively, extract its code-cell sources joined with `GO` and pipe them through the same `sqlcmd` invocation shown above.
```

- [ ] **Step 5: Verify lesson 13's directory has exactly four files**

Run: `ls lessons/13-execution-plans-and-tuning/`
Expected: `exercises-solutions.ipynb  exercises.ipynb  lesson.ipynb  setup.sql`

- [ ] **Step 6: Commit**

```bash
git add CLAUDE.md
git commit -m "feat: lesson 13 notebook pilot - retire old files, update conventions"
```

---

### Task 6: Manual ADS validation gate (user)

The spec's pilot acceptance criteria require Azure Data Studio's UI, which cannot be scripted. This task is a checklist for the user; the implementer's job is to present it and act on any failures.

**Files:**
- None (fixes, if any, loop back into the notebooks from Tasks 2–4).

**Interfaces:**
- Consumes: the four-file lesson 13 layout committed in Task 5.
- Produces: pilot sign-off (spec: "The pilot is considered validated once…"), unblocking the 17-lesson rollout as future work.

- [ ] **Step 1: Present this checklist to the user and ask them to run it in Azure Data Studio**

1. Open `lessons/13-execution-plans-and-tuning/lesson.ipynb` in ADS — it renders without errors, markdown tables display correctly.
2. Attach the kernel to the `mssql-learn` localhost connection; run `setup.sql` first (via the documented sqlcmd pipe or ADS query window).
3. Run all cells top to bottom — every code cell succeeds; result grids and `STATISTICS IO/TIME` messages render inline.
4. Repeat open/attach/run-all for `exercises.ipynb` (starter cells run; placeholder cells are editable) and `exercises-solutions.ipynb` (all cells run).
5. Confirm the SSMS hand-off notes read sensibly at the points where the graphical plan viewer is needed.
6. Confirm "Clear All Outputs" before commit leaves no output noise in `git diff`.

- [ ] **Step 2: Fix any issues the user reports, re-verify with the Task 2–4 harness, and commit fixes as `fix: lesson 13 notebook pilot - <issue>`**

- [ ] **Step 3: On sign-off, note follow-up work (not this plan): apply the same pattern to lessons 01–12 and 14–18**
