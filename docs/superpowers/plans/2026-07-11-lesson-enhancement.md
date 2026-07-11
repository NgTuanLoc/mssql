# Lesson Enhancement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bring all 18 MSSQL lessons up to the capstone's quality bar: deep READMEs (mental models, inline sample output, pitfalls with demos) plus 2–4 realistic CHALLENGE exercises per lesson with reasoning-path solutions.

**Architecture:** Pure content work on Markdown + SQL files — no application code. Each lesson is an independent unit (README.md, setup.sql, examples.sql, exercises.sql, exercises-solutions.sql) enhanced and verified against a live MSSQL 2022 container, then committed. Pilot lesson 07 first with a mandatory user review gate; the approved pilot becomes the style reference for the remaining 17 lessons.

**Tech Stack:** MSSQL 2022 Developer in Docker (container `mssql-learn`, database AdventureWorks2022), sqlcmd inside the container, Git Bash on Windows.

**Spec:** `docs/superpowers/specs/2026-07-11-lesson-enhancement-design.md` — read it before starting.

## Global Constraints

- **Five-file convention holds:** every lesson dir contains exactly `README.md`, `setup.sql`, `examples.sql`, `exercises.sql`, `exercises-solutions.sql`. No new files in lesson dirs.
- **Existing exercises are preserved verbatim** — never edit, renumber, or delete an existing exercise or its solution. Only append.
- **setup.sql stays idempotent:** drop all tables in the `lessonNN` schema, drop schema, recreate. If a challenge needs seeded data AdventureWorks lacks, add it to setup.sql inside the existing drop-recreate pattern.
- **README target: 150–250 lines**, scaled to topic weight (lesson 01 lighter, 13 heavier). Keep and expand existing accurate content — rework, not rewrite.
- **Sample outputs must be real:** every inline output block in a README is captured from an actual container run, trimmed to the first 3–6 rows. Never invent output.
- **Challenge difficulty comes from combination and realism**, not obscure syntax. Challenges may use concepts from earlier lessons only (never later ones).
- **Regular-exercise solutions keep the 1–2 line comment convention. Challenge solutions get a 3–6 line reasoning-path comment** (decomposition, technique choice and why, the tempting-but-wrong approach).
- **Commits:** one per lesson, message `feat: enhance lesson NN - deep README + challenge exercises` (conventional commits; no attribution footer — disabled globally).
- **Verification is the test cycle:** SQL that hasn't run cleanly against `mssql-learn` does not get committed.

## Standard Lesson Workflow (referenced by every lesson task)

Every lesson task follows this procedure. The per-task sections below supply the lesson-specific content (README directives, challenge texts); this section supplies the mechanics.

### A. Environment (once per session)

```bash
cd /d/Study/Database/mssql
docker compose -f docker/docker-compose.yml up -d
SA_PASSWORD=$(sed -n 's/^SA_PASSWORD=//p' docker/.env | tr -d '\r')
# Smoke test — expect one row: AdventureWorks2022
echo "SELECT name FROM sys.databases WHERE name='AdventureWorks2022';" | \
  docker exec -i mssql-learn /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -No
```

Define a helper for the rest of the session:

```bash
runsql() { docker exec -i mssql-learn /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -No -b; }
# usage:  cat lessons/NN-topic/file.sql | runsql        (-b: nonzero exit on SQL error)
# ad-hoc: echo "SELECT ...;" | runsql
```

### B. README rework template

Rework the lesson README to exactly this section order (from the spec):

```markdown
# Lesson NN — Topic

**Prerequisites:** <lessons it builds on, one line>

<2–3 sentence motivating hook: the real problem this solves, phrased as
something a developer actually hits — not a feature list.>

## What you'll learn
<existing bullets, tightened>

## The mental model
<the ONE diagram or analogy explaining HOW it works. Mermaid for
flows/structures; ASCII tables where physical layout matters.>

## Setup
<unchanged convention: run setup.sql once, what it creates>

## Concepts
### 1. <Concept>
<minimal runnable snippet → real trimmed output in a fenced block →
one paragraph of why/when → ends with "**Reach for this when** ..." line>

## Worked examples
<one line per examples.sql entry: what it shows AND what to notice
in its output>

## Pitfalls
<each pitfall gets a short "see it break" snippet where practical,
with the broken behaviour shown or described precisely>

## Exercises
Open `exercises.sql` and try them in order. Solutions in
`exercises-solutions.sql`. The CHALLENGES section at the bottom is
harder: those problems combine this lesson's topic with earlier
lessons and usually need 2+ concepts in one query.

## Cheatsheet link
<unchanged, where present>
```

### C. Challenge block format

Append to the END of `exercises.sql` (existing content untouched):

```sql
------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: <text from the task's Challenge table below>
-- Expected columns: <from the table>
-- Hint: <only where the task marks one>
-- Your query here:

```

Append matching solutions to the END of `exercises-solutions.sql`, mirroring the same header banner, each solution preceded by its reasoning-path comment:

```sql
-- Challenge 1: <restate title>
-- Reasoning: <3–6 lines: how to decompose it; which technique and why;
--            what the tempting-but-wrong approach is and why it fails.>
<the query>
```

### D. Per-lesson verification loop

```bash
# 1. Setup must run clean (idempotent — run it twice to prove it)
cat lessons/NN-topic/setup.sql | runsql && cat lessons/NN-topic/setup.sql | runsql

# 2. examples.sql if touched
cat lessons/NN-topic/examples.sql | runsql

# 3. Full solutions file — old and new solutions all execute
cat lessons/NN-topic/exercises-solutions.sql | runsql

# 4. Every new README snippet: paste each into `runsql` individually,
#    capture the output, trim to 3–6 rows, paste into the README.
```

Expected: exit code 0, no `Msg NNNN` error lines (except where a pitfall demo intentionally errors — run those interactively, not under `-b`, and confirm the error is the one documented). Spot-check each challenge solution's result set against its stated Expected columns and plausibility (e.g. a "top 3 per group" result has ≤3 rows per group).

### E. Commit

```bash
git add lessons/NN-topic/
git commit -m "feat: enhance lesson NN - deep README + challenge exercises"
```

### F. Canonical step sequence ("Steps 1–6")

Where a lesson task says "Steps 1–6", it means exactly:

1. Read all five current lesson files end to end.
2. Rework README.md per the task's directives (template in section B); leave `{OUTPUT}` markers where sample output will go.
3. Append the CHALLENGES block (section C) to exercises.sql with the task's challenge texts.
4. Write challenge solutions with reasoning-path comments in exercises-solutions.sql; extend setup.sql idempotently if the task's Files line includes it.
5. Run the verification loop (section D); replace every `{OUTPUT}` marker with real captured output; re-run until clean — zero markers remain.
6. Commit per section E with the task's commit message.

---

### Task 1: Preflight + leftover cleanup

**Files:**
- Delete: `lessons/18-diagnosing-common-issues/` (empty, untracked)

- [ ] **Step 1: Bring up the environment** — run section A of the Standard Workflow. Expect the smoke test to return `AdventureWorks2022`. If the container or database is missing, restore via `scripts/win/restore-adventureworks.ps1` (see `data/adventureworks/README.md`).
- [ ] **Step 2: Delete the leftover directory**

```bash
rmdir lessons/18-diagnosing-common-issues
git status   # expect: clean (dir was untracked and empty — nothing to commit)
```

- [ ] **Step 3: Confirm baseline** — `git log --oneline -1` shows the spec commit; working tree clean.

*(No commit — the directory is untracked, so its removal produces no git change. If git status is NOT clean, stop and report.)*

---

### Task 2: PILOT — Lesson 07 (window functions) + user review gate

**Files:**
- Modify: `lessons/07-window-functions/README.md` (currently 65 lines)
- Modify: `lessons/07-window-functions/exercises.sql` (5 exercises, preserved)
- Modify: `lessons/07-window-functions/exercises-solutions.sql`
- Modify: `lessons/07-window-functions/setup.sql` — only if Challenge 1 needs seeding beyond `lesson07.MonthlySales`; check that table's shape first.

**README directives** (section B template):
- Hook: "Show each order next to its customer's running total" — the class of question GROUP BY can't answer without losing detail rows.
- Mental model: a mermaid or ASCII picture of one partition — rows sorted, a frame sliding over them, the function computing per-row *without collapsing rows*. Contrast in one line with GROUP BY (collapses) vs window (annotates).
- Concepts to build stepwise with real output: (1) `OVER()` anatomy — partition/order/frame; (2) ranking family with a tie in the data so RANK vs DENSE_RANK vs ROW_NUMBER visibly differ in the output block; (3) offset functions LAG/LEAD; (4) aggregate windows + framing — running total, then moving average, showing the frame clause is the only difference; (5) FIRST_VALUE/LAST_VALUE with the default-frame trap shown.
- Pitfalls to demo: LAST_VALUE default frame (show wrong output, then fixed); RANGE vs ROWS with duplicate ORDER BY values (show the tie-inflated running total); window function in WHERE (show the error message).

**Challenges to append** (section C format):

| # | Text | Expected columns | Hint |
|---|------|------------------|------|
| 1 | Using `Sales.SalesOrderHeader`, find each salesperson's longest streak of consecutive months with month-over-month increasing total sales. A streak breaks on a down-or-flat month or a gap month. Report each salesperson's best streak length and the months it spanned. | SalesPersonID, StreakLengthMonths, StreakStart, StreakEnd | Aggregate to salesperson-month first; LAG for the comparison; the difference of two ROW_NUMBERs (or a running flag-sum) groups consecutive rising months — gaps-and-islands. |
| 2 | For each customer with 5+ orders: their top-3 orders by TotalDue and each of those orders' percentage of that customer's lifetime spend. | CustomerID, SalesOrderID, TotalDue, PctOfLifetimeSpend, OrderRank | — |
| 3 | Using `lesson07.MonthlySales`: each region's 3-month moving average of revenue, plus a flag column marking months where revenue fell more than 20% below that moving average. | Region, SaleYear, SaleMonth, Revenue, MovAvg3, IsSlumpMonth | — |
| 4 | Split all customers into spending quartiles with NTILE(4) over lifetime TotalDue, then report one row per quartile: customer count, min/avg/max lifetime spend. (Two window layers: NTILE, then aggregate the result.) | SpendQuartile, CustomerCount, MinSpend, AvgSpend, MaxSpend | — |

- [ ] **Step 1: Read all five current lesson 07 files** end to end.
- [ ] **Step 2: Rework README.md** per directives; leave `{OUTPUT}` markers where sample output will go.
- [ ] **Step 3: Append CHALLENGES block** to exercises.sql with the four texts above.
- [ ] **Step 4: Write the four challenge solutions** with reasoning-path comments in exercises-solutions.sql.
- [ ] **Step 5: Run the verification loop** (section D). Capture real outputs, replace every `{OUTPUT}` marker. Re-run until clean — README must contain zero markers.
- [ ] **Step 6: Commit** — `feat: enhance lesson 07 - deep README + challenge exercises`
- [ ] **Step 7: STOP — user review gate.** Present the reworked README and challenge set to the user. Do NOT start any other lesson task until the user approves the style. Fold any requested adjustments into lesson 07, re-verify, commit as `fix: adjust lesson 07 enhancement per review`, and record the adjustments as amendments to the Global Constraints for all remaining tasks.

---

### Task 3: Lesson 01 — setup and basics

**Files:** `lessons/01-setup-and-basics/` — README.md, exercises.sql, exercises-solutions.sql

**README directives:** lighter end of the range (~120–150 lines). Hook: you have a running server and no idea what's in it. Mental model: client → TDS connection → database → schema → table, as a small mermaid diagram (where a query "lands"). Concepts with output: SELECT anatomy and logical order of TOP/WHERE/ORDER BY; exploring structure with `sp_help` and `INFORMATION_SCHEMA.COLUMNS`. Pitfalls: TOP without ORDER BY is nondeterministic (run twice, note rows may differ); case-insensitive default collation surprise.

**Challenges:**

| # | Text | Expected columns | Hint |
|---|------|------------------|------|
| 1 | Using `INFORMATION_SCHEMA.COLUMNS`, list every table in AdventureWorks2022 that has a column named `ModifiedDate`, with the table's schema and total column count. | TableSchema, TableName, ColumnCount | Filter for the column, then aggregate per table. |
| 2 | Show the 10 most expensive products, but include ties: if products 10 and 11 share a price, show both. Compare row counts with and without `WITH TIES`. | Name, ListPrice | TOP (10) WITH TIES requires ORDER BY. |
| 3 | Find the 5 largest tables in the database by row count using `sys.tables`, `sys.schemas` and `sys.partitions` (index_id 0 or 1). | SchemaName, TableName, RowCnt | — |

- [ ] Step 1: Read current files. Step 2: Rework README. Step 3: Append challenges. Step 4: Write solutions with reasoning comments. Step 5: Verification loop (setup.sql ×2, solutions, every new snippet; capture real output). Step 6: Commit `feat: enhance lesson 01 - deep README + challenge exercises`.

---

### Task 4: Lesson 02 — T-SQL core SELECT

**Files:** `lessons/02-tsql-core-select/` — README.md, exercises.sql, exercises-solutions.sql

**README directives:** Hook: the report page that shows wrong rows because of NULL three-valued logic. Mental model: logical query processing order — FROM → WHERE → SELECT → ORDER BY → TOP/OFFSET as an ASCII pipeline, explaining why you can't use a SELECT alias in WHERE. Concepts with output: predicate toolkit (LIKE/IN/BETWEEN); NULL three-valued logic with a truth-table snippet; ORDER BY determinism; OFFSET/FETCH paging. Pitfalls demoed: `Color <> 'Red'` silently dropping NULL rows (show counts); `NOT IN` with a NULL in the list returning zero rows; BETWEEN on datetime end-of-day trap.

**Challenges:**

| # | Text | Expected columns | Hint |
|---|------|------------------|------|
| 1 | Build page 3 of a product listing: 10 rows per page, ordered by ListPrice DESC. The ordering must be fully deterministic even when prices tie. | ProductID, Name, ListPrice | Add a unique tie-breaker to ORDER BY; OFFSET 20 ROWS FETCH NEXT 10. |
| 2 | Return all products that are NOT red — where "not red" must include products whose Color is unknown (NULL). Then explain (comment) why the naive `<> 'Red'` version returns fewer rows. | ProductID, Name, Color | — |
| 3 | Find products whose Name contains a literal percent sign or underscore anywhere, using an ESCAPE clause. Seed check: if none exist in AdventureWorks, search for names containing a hyphen followed by a digit instead (e.g. 'Mountain-100'). | ProductID, Name | LIKE '%!%%' ESCAPE '!' |
| 4 | List the orders placed in the last 30 days of available data — "last 30 days" computed from `MAX(OrderDate)`, not hardcoded — using a half-open range `>= start AND < end`, never BETWEEN. | SalesOrderID, OrderDate, TotalDue | Scalar subquery or variable for the max date. |

- [ ] Step 1: Read current files. Step 2: Rework README. Step 3: Append challenges. Step 4: Write solutions with reasoning comments. Step 5: Verification loop (section D). Step 6: Commit `feat: enhance lesson 02 - deep README + challenge exercises`.

---

### Task 5: Lesson 03 — data types and expressions

**Files:** `lessons/03-data-types-and-expressions/` — README.md, exercises.sql, exercises-solutions.sql, setup.sql (add a `lesson03.MixedDates` seed table for Challenge 4 if setup doesn't already provide messy strings — keep idempotent).

**README directives:** Hook: a WHERE clause that stopped using the index because of one `N` prefix. Mental model: the type-precedence ladder (which side of a comparison gets converted) as a small ASCII diagram — implicit conversion always converts the *lower*-precedence side, and if that's the column, seeks die. Concepts with output: exact vs approximate numerics, DECIMAL scale arithmetic; VARCHAR vs NVARCHAR and the N prefix; DATETIME2 vs DATETIME precision (show the .003 rounding); CAST/CONVERT styles and TRY_ variants (show a NULL from TRY_CAST). Pitfalls demoed: MONEY division rounding error accumulating vs DECIMAL; implicit conversion warning — NVARCHAR literal vs VARCHAR column; string date '2014-02-31' with CAST (error) vs TRY_CAST (NULL).

**Challenges:**

| # | Text | Expected columns | Hint |
|---|------|------------------|------|
| 1 | Demonstrate the MONEY rounding trap: split $100.00 into 3 equal parts with MONEY and with DECIMAL(19,4), sum the parts back, and show the difference in cents in one result row. | MoneyTotal, DecimalTotal, MoneyLostCents | — |
| 2 | Group all 2013 orders into fiscal quarters where the fiscal year starts July 1 (Q1 = Jul–Sep). Compute the fiscal quarter from OrderDate with date functions — no CASE ladder over month numbers. | FiscalYear, FiscalQuarter, OrderCount, TotalDue | DATEADD the date back 6 months, then DATEPART quarter/year of the shifted date. |
| 3 | Using `Person.Person`, produce a padded display name exactly 40 chars wide: `LastName, FirstName` left-aligned, padded with dots. Show LEN vs DATALENGTH for 5 rows and explain the factor of 2 in a comment. | DisplayName, LenChars, DataLengthBytes | NVARCHAR: DATALENGTH = 2× LEN. |
| 4 | `lesson03.MixedDates` (seeded in setup.sql) holds date strings in mixed formats ('2014-03-01', '03/15/2014', '20140401', 'not a date'). Produce a clean report: original string, parsed DATE where possible (trying styles 120, 101, 112 in that order), and a flag for unparseable rows. | RawValue, ParsedDate, IsValid | COALESCE over TRY_CONVERT calls with explicit styles. |

- [ ] Steps 1–6 (setup.sql change included in verification: run twice; commit `feat: enhance lesson 03 - deep README + challenge exercises`).

---

### Task 6: Lesson 04 — joins and sets

**Files:** `lessons/04-joins-and-sets/` — README.md, exercises.sql, exercises-solutions.sql

**README directives:** Hook: the revenue report that doubled overnight because someone joined one more table. Mental model: mermaid diagram of a LEFT JOIN as row matching — preserved side vs matched side — plus the fan-out picture (1 header row × 3 detail rows = 3 output rows). Concepts with output: join types with tiny result sets showing NULL padding; multi-join chains; the ON vs WHERE distinction for outer joins (show the "accidental inner join" row-count change); UNION ALL vs UNION cost/dedup, INTERSECT/EXCEPT NULL handling. Pitfalls demoed: fan-out double counting (show inflated SUM vs correct); filtering the outer table in WHERE turning LEFT into INNER; UNION dropping duplicate rows you wanted.

**Challenges:**

| # | Text | Expected columns | Hint |
|---|------|------------------|------|
| 1 | The fan-out trap, fixed: total 2013 revenue per product from `Sales.SalesOrderDetail` joined with per-order freight share. First write the naive join of header+detail that inflates freight, show the wrong total in a comment, then fix it by pre-aggregating detail per order in a derived table/CTE. | ProductID, Revenue, FreightShare | Aggregate before joining, not after. |
| 2 | Customers who ordered in 2013 but not in 2014 — solve it twice: once with EXCEPT, once with a LEFT JOIN ... IS NULL anti-join, and confirm both return the same row count. | CustomerID | — |
| 3 | Full reconciliation: every product with its total quantity sold via online orders and via offline orders side by side, including products sold through only one channel or neither. | ProductID, Name, OnlineQty, OfflineQty | Two pre-aggregated sets, FULL OUTER JOIN or LEFT JOINs from Product; COALESCE the quantities to 0. |
| 4 | Non-equi join: build a price-band reference with VALUES (('Budget',0,100),('Mid',100,1000),('Premium',1000,10000)) and count products per band, joining on a range predicate — no CASE expression. | BandName, ProductCount | JOIN ... ON ListPrice >= low AND ListPrice < high. |

- [ ] Steps 1–6 (commit `feat: enhance lesson 04 - deep README + challenge exercises`).

---

### Task 7: Lesson 05 — aggregations and grouping

**Files:** `lessons/05-aggregations-and-grouping/` — README.md, exercises.sql, exercises-solutions.sql

**README directives:** Hook: "average order value" computed as AVG of AVGs — plausible, confidently wrong. Mental model: GROUP BY as bucketing — ASCII picture of rows falling into buckets, aggregates computed per bucket, detail rows gone (contrast one line with lesson 07 windows). Concepts with output: aggregate + NULL behaviour (COUNT(*) vs COUNT(col) on a column with NULLs — show differing counts); HAVING vs WHERE placement; GROUPING SETS/ROLLUP with GROUPING_ID labelling subtotal rows in the output; conditional aggregation (CASE inside SUM) as the poor-man's pivot. Pitfalls demoed: AVG ignores NULLs (show AVG vs SUM/COUNT(*) differing); non-aggregated column in SELECT (show error 8120); ROLLUP NULLs vs real NULLs disambiguated with GROUPING().

**Challenges:**

| # | Text | Expected columns | Hint |
|---|------|------------------|------|
| 1 | One result set with revenue at three levels — per (territory, year), per territory subtotal, and grand total — using GROUPING SETS, with a readable Level column ('Detail'/'Territory total'/'Grand total') derived from GROUPING_ID. | TerritoryName, OrderYear, Revenue, Level | — |
| 2 | The correct "average order value" per territory: SUM(TotalDue)/COUNT(DISTINCT SalesOrderID), shown next to the naive AVG(TotalDue) over a header+detail join, with a comment explaining why they differ. | TerritoryName, NaiveAvg, CorrectAvg | The join fans out headers; AVG then weights big orders by line count. |
| 3 | A matrix of order counts: one row per territory, one column per year 2011–2014, built with conditional aggregation (SUM(CASE...)). | TerritoryName, Y2011, Y2012, Y2013, Y2014 | — |
| 4 | Territories whose 2013 revenue exceeded their 2012 revenue by more than 10% — aggregate per territory-year first, then compare years. Decide (and justify in the comment) whether the comparison belongs in HAVING or in an outer query. | TerritoryName, Rev2012, Rev2013, GrowthPct | Conditional aggregation puts both years on one row. |

- [ ] Steps 1–6 (commit `feat: enhance lesson 05 - deep README + challenge exercises`).

---

### Task 8: Lesson 06 — subqueries and CTEs

**Files:** `lessons/06-subqueries-and-ctes/` — README.md, exercises.sql, exercises-solutions.sql

**README directives:** Hook: the query you can't read six months later — nesting vs naming. Mental model: mermaid diagram of a recursive CTE loop (anchor → recursive member referencing itself → union until empty), plus one line on CTEs as named building blocks (foreshadow lesson 18). Concepts with output: scalar subqueries (and the more-than-one-row error, shown); IN vs EXISTS semantics with NULL (NOT IN + NULL trap shown with counts); correlated subqueries and how to read them inside-out; non-recursive CTE refactor of a nested query (before/after, same output); recursive CTE over `HumanResources.Employee` hierarchy with MAXRECURSION note. Pitfalls demoed: NOT IN returning nothing when subquery yields a NULL; CTE re-executed per reference (point at lesson 18's note); scalar subquery per row = hidden N+1.

**Challenges:**

| # | Text | Expected columns | Hint |
|---|------|------------------|------|
| 1 | Full management chain: for every employee, a path string from the CEO down to them ('CEO > VP > Manager > Employee') and their depth, via recursive CTE over `HumanResources.Employee`'s OrganizationNode/JobTitle (parent = direct manager). | BusinessEntityID, JobTitle, Depth, ChainPath | Build the path in the recursive member: parent.ChainPath + ' > ' + child title; CAST path as NVARCHAR(4000) in both members to match types. |
| 2 | Products that have NEVER been ordered by a store (Sales.Customer.StoreID IS NOT NULL) — with NOT EXISTS. In the comment, explain why NOT IN would be risky here. | ProductID, Name | — |
| 3 | Generate a complete month calendar for 2013 (12 rows) with a recursive CTE, then LEFT JOIN monthly order totals so months with zero orders still show — verify no month is missing. | MonthStart, OrderCount, Revenue | Anchor '2013-01-01', DATEADD month +1 until '2013-12-01'. |
| 4 | For each territory, the single customer with the highest lifetime TotalDue — solved with a correlated subquery in WHERE, and a comment on which lesson-07 pattern replaces this more efficiently. | TerritoryID, CustomerID, LifetimeSpend | WHERE spend = (SELECT MAX ... same territory). |

- [ ] Steps 1–6 (commit `feat: enhance lesson 06 - deep README + challenge exercises`).

---

### Task 9: Lesson 08 — DML

**Files:** `lessons/08-dml-insert-update-delete-merge/` — README.md, exercises.sql, exercises-solutions.sql, setup.sql (check what lesson08 tables exist; challenges below assume a target/staging pair — add `lesson08.ProductPriceStage` / audit table to setup if absent, idempotent).

**README directives:** Hook: the UPDATE that hit every row because the FROM join fanned out. Mental model: every DML statement as read-then-write — the reading half is a full SELECT with all its join semantics; ASCII of UPDATE...FROM as a join feeding an update stream. Concepts with output: INSERT forms; UPDATE with FROM (and its nondeterminism when the join is 1:N — stated precisely); DELETE with join; OUTPUT clause capturing inserted./deleted. (show captured rows); MERGE anatomy with $action. Pitfalls demoed: UPDATE...FROM fan-out picking an arbitrary row (two-row demo); MERGE without HOLDLOCK race note + the classic "MERGE quirks" caveat list; forgetting WHERE on UPDATE/DELETE (demo inside a rolled-back transaction).

**Challenges:**

| # | Text | Expected columns | Hint |
|---|------|------------------|------|
| 1 | Idempotent load: insert into the lesson08 target only the staging rows not already present (key match), capturing inserted keys via OUTPUT into a table variable and selecting them. Run it twice — second run must insert 0 rows. | (inserted keys result set) | INSERT ... SELECT ... WHERE NOT EXISTS. |
| 2 | Full sync with audit: MERGE staging into target (update changed, insert new, delete missing) writing $action plus old/new values via OUTPUT into an audit table. Verify the audit row counts match the changes you staged. | (audit table contents) | OUTPUT $action, deleted.*, inserted.*. |
| 3 | Dedupe in place: seed duplicate rows (same natural key, different ModifiedDate) in a lesson08 table, then delete all but the newest per key using a CTE over ROW_NUMBER — a windowed DELETE. Prove with a count per key = 1. | (post-delete verification counts) | DELETE FROM cte WHERE rn > 1. |
| 4 | Batch delete: delete a large set in chunks of 500 rows in a WHILE loop (using TOP), reporting rows deleted per iteration, and explain in the comment why chunking beats one giant DELETE (lock escalation, log growth — lessons 16/17 preview). | (iteration report via PRINT/SELECT) | WHILE @@ROWCOUNT ... DELETE TOP (500). |

- [ ] Steps 1–6 (setup.sql runs twice; commit `feat: enhance lesson 08 - deep README + challenge exercises`).

---

### Task 10: Lesson 09 — DDL and constraints

**Files:** `lessons/09-ddl-and-constraints/` — README.md, exercises.sql, exercises-solutions.sql

**README directives:** Hook: bad data is a thousand times more expensive to remove than to reject — constraints as the last line of defence no application bug can bypass. Mental model: ASCII table of the constraint toolbox — what each constraint rejects, when it's checked, and its error number. Concepts with output: CREATE TABLE with all constraint types; IDENTITY vs SEQUENCE (show a shared sequence across two tables); computed columns and PERSISTED; ALTER TABLE add-column patterns; FK referential actions (NO ACTION vs CASCADE, one demo). Pitfalls demoed: each constraint type violated on purpose — show the exact error message for PK dup, FK orphan, CHECK reject; NULL slips past a CHECK constraint (UNKNOWN passes!); adding NOT NULL column to a populated table without DEFAULT (error), then the right way.

**Challenges:**

| # | Text | Expected columns | Hint |
|---|------|------------------|------|
| 1 | Design `lesson09.Enrollment` (StudentID, CourseID, EnrolledOn, Grade): composite PK, FKs to seeded Student/Course tables, CHECK on Grade IN ('A','B','C','D','F') allowing NULL for in-progress, DEFAULT SYSDATETIME() for EnrolledOn. Then write one INSERT per constraint that violates it, each in a comment noting the expected error number. | (DDL + violation demos) | — |
| 2 | One SEQUENCE feeding TicketID defaults in two tables (OnlineTicket, CounterTicket); insert into both alternately and show the interleaved global ordering. In the comment: why IDENTITY cannot do this. | TicketID, Source | — |
| 3 | Add computed column TotalPrice AS (Quantity * UnitPrice) PERSISTED to a seeded order-lines table, plus a CHECK that it's <= 10000. Demonstrate the CHECK firing via an UPDATE to Quantity. | (DDL + demo) | Constraint on the computed column is enforced through its inputs. |
| 4 | Safely add a NOT NULL Status column (default 'Active') to a populated table three ways attempted: (a) plain ADD Status NVARCHAR(10) NOT NULL — show the failure, (b) ADD with DEFAULT constraint — works, (c) nullable-add + backfill + ALTER to NOT NULL. Comment on when (c) beats (b) for huge tables. | (DDL sequence) | — |

- [ ] Steps 1–6. Intentional-error demos run interactively without `-b`, confirming documented error numbers. Commit `feat: enhance lesson 09 - deep README + challenge exercises`.

---

### Task 11: Lesson 10 — views, procedures, functions

**Files:** `lessons/10-views-procedures-functions/` — README.md, exercises.sql, exercises-solutions.sql

**README directives:** Hook: the report that got 40× slower because a scalar UDF ran once per row. Mental model: ASCII decision table — view (named query, inlined), iTVF (parameterised view, inlined), scalar UDF (row-by-row black box), proc (program, not composable in queries) — one row each: composability, inlining, side effects. Concepts with output: CREATE OR ALTER VIEW + WITH CHECK OPTION demo; proc conventions (SET NOCOUNT ON, typed params, OUTPUT param, return codes); scalar UDF cost — same logic as UDF vs inline expression with STATISTICS TIME comparison; iTVF + CROSS APPLY as the top-N-per-group tool. Pitfalls demoed: SELECT * view breaking silently after column reorder/add (sp_refreshview); scalar UDF killing parallelism (stated, with the timing evidence above); ORDER BY inside a view (error / meaningless).

**Challenges:**

| # | Text | Expected columns | Hint |
|---|------|------------------|------|
| 1 | Write `lesson10.ufn_TopOrdersForCustomer(@CustomerID, @N)` as an iTVF returning the customer's top-N orders; use CROSS APPLY to run it over all customers in territory 1 with @N=3. | CustomerID, SalesOrderID, TotalDue, OrderRank | — |
| 2 | Proc `lesson10.usp_TerritoryRevenue` (@TerritoryID, @Year, @Revenue OUTPUT, returns 0 ok / 1 not found): TRY/CATCH, SET NOCOUNT ON, validated params (THROW 50001 on NULL inputs). Demo the success call, the not-found return code, and the thrown validation error. | (three demo calls) | — |
| 3 | Rewrite a scalar UDF as inline logic: create ufn_CustomerLifetimeSpend(@CustomerID) scalar UDF, use it in a query over all customers with STATISTICS TIME on; then the same result via one windowed/grouped query. Record both CPU times in the solution comment. | CustomerID, LifetimeSpend | — |
| 4 | Updatable-view boundary: create a single-table view over lesson10 data WITH CHECK OPTION filtering Status='Active'; show an UPDATE through the view that succeeds, one that violates the filter (error 550), and explain in a comment what would make the view non-updatable (joins, aggregates, DISTINCT). | (demo sequence) | — |

- [ ] Steps 1–6 (commit `feat: enhance lesson 10 - deep README + challenge exercises`).

---

### Task 12: Lesson 11 — transactions, errors, concurrency

**Files:** `lessons/11-transactions-errors-concurrency/` — README.md, exercises.sql, exercises-solutions.sql

**README directives:** Hook: the payment saved, the ledger insert failed, and nothing rolled back. Mental model: mermaid state diagram of a transaction (active → committed / rolled back, with XACT_STATE() -1 "doomed" state) — errors don't automatically roll back without XACT_ABORT. Concepts with output: explicit transactions + @@TRANCOUNT (show nesting is a lie — one real transaction); TRY/CATCH + THROW rethrow pattern; ERROR_* functions in the CATCH; XACT_ABORT ON as the sane default; isolation levels ladder with the anomaly each prevents (ASCII table); the two-session deadlock walkthrough (existing content — keep and expand). Pitfalls demoed: error without XACT_ABORT leaving the transaction open (show @@TRANCOUNT stuck at 1); nested BEGIN TRAN + ROLLBACK rolling back everything; RAISERROR vs THROW severity/rethrow differences.

**Challenges:** (two-session challenges give exact per-session scripts; verified per section D two-session pattern)

| # | Text | Expected columns | Hint |
|---|------|------------------|------|
| 1 | Deadlock-retry wrapper: a proc that performs a two-table update inside a retry loop — on error 1205 it waits (WAITFOR DELAY '00:00:00.5') and retries up to 3 times, rethrowing anything else. Include the full TRY/CATCH/retry skeleton and a comment on why only 1205 is retryable. | (proc DDL + demo call) | Loop with @retry counter; CATCH checks ERROR_NUMBER() = 1205. |
| 2 | Doomed-transaction handling: with XACT_ABORT ON, hit a conversion error inside a transaction and show XACT_STATE() = -1 in CATCH — prove you can only ROLLBACK, not COMMIT (attempt COMMIT, show error 3930). | (demo sequence) | — |
| 3 | Two-session RCSI observation: Session A updates a lesson11 row inside an open transaction; Session B reads it under READ COMMITTED (blocks) vs under SNAPSHOT (sees old value instantly). Provide both session scripts and the expected observation at each step. | (two scripts + narrated expectations) | Check RCSI/snapshot settings on AdventureWorks2022 first (sys.databases); ALTER DATABASE ... SET ALLOW_SNAPSHOT_ISOLATION ON if needed, and revert after. |
| 4 | Savepoints: one transaction inserting a batch of 3 orders where the 2nd fails validation — SAVE TRANSACTION before each, roll back only the failed one, commit the rest, prove 2 of 3 rows persisted. | (final row count) | SAVE TRANSACTION sp; ROLLBACK TRANSACTION sp. |

- [ ] Steps 1–6. Two-session scripts verified via two parallel `docker exec` sqlcmd invocations (run session A in background with `&`, session B foreground, compare observed blocking to the narrated expectations). Commit `feat: enhance lesson 11 - deep README + challenge exercises`.

---

### Task 13: Lesson 12 — indexes

**Files:** `lessons/12-indexes/` — README.md, exercises.sql, exercises-solutions.sql, setup.sql (challenges need a copyable table — check existing lesson12 setup; add `lesson12.OrderCopy` seeded from Sales.SalesOrderHeader if absent).

**README directives:** Hook: the query reads 1.2 million pages to return 40 rows. Mental model: the B-tree as a phone book — ASCII of root/intermediate/leaf pages; clustered = the book itself sorted, nonclustered = a separate index in the back pointing at pages; key lookup = flipping back and forth per row. Concepts with output: seek vs scan with SET STATISTICS IO logical-read counts shown; composite key column order (equality first, then range — show a seek vs scan flip when order is wrong); INCLUDE covering a query (before/after lookup elimination with read counts); filtered indexes; the two DMVs for usage and fragmentation. Pitfalls demoed: index unused because predicate wraps the column in a function (bridge to lesson 13); over-indexing write cost (stated with insert-timing sketch); fragmentation panic — when rebuilds don't matter.

**Challenges:**

| # | Text | Expected columns | Hint |
|---|------|------------------|------|
| 1 | Workload-driven design: given three queries against lesson12.OrderCopy — (a) WHERE CustomerID = ? ORDER BY OrderDate DESC, (b) WHERE OrderDate range + SalesPersonID equality returning TotalDue, (c) WHERE Status = 5 AND OnlineOrderFlag = 1 (99% of rows are Status 5) — design a MINIMAL index set. Justify each key column's position and each INCLUDE in comments; prove each query seeks via STATISTICS IO before/after counts recorded in comments. | (index DDL + evidence comments) | (b): equality column before range column in the key. (c): candidate for a filtered index — or none at all; justify either way. |
| 2 | Key-lookup elimination: write a query that seeks but shows a Key Lookup (predicate on indexed column, SELECT includes non-key columns); record logical reads; add INCLUDE columns; record reads again. Numbers go in the solution comments. | (before/after evidence) | — |
| 3 | Index health report: one query joining sys.dm_db_index_usage_stats + sys.indexes over AdventureWorks2022 listing every index's reads (seeks+scans+lookups) vs writes, flagging candidates WHERE writes > 0 AND reads = 0. | SchemaName, TableName, IndexName, Reads, Writes, Flag | Usage stats reset on restart — note it in the comment. |
| 4 | Filtered index for the soft-delete pattern: add IsDeleted to lesson12.OrderCopy (99% = 0), create a filtered index WHERE IsDeleted = 0, then show one query that uses it and one (querying IsDeleted = 1... or a parameterised predicate) that cannot, explaining why in the comment. | (demo + explanation) | Parameterised predicates can't match a filtered index at compile time. |

- [ ] Steps 1–6 (setup ×2; STATISTICS IO evidence captured from real runs into solution comments; commit `feat: enhance lesson 12 - deep README + challenge exercises`).

---

### Task 14: Lesson 13 — execution plans and tuning

**Files:** `lessons/13-execution-plans-and-tuning/` — README.md, exercises.sql, exercises-solutions.sql, setup.sql (parameter-sniffing challenge needs a skewed table — check existing lesson13 setup for one; add `lesson13.SkewedOrders` — e.g. 100k rows where one CustomerID owns 90% — if absent).

**README directives:** Hook: "it's fast in SSMS but slow in production" — the sentence that pays for this lesson. Mental model: a plan as a right-to-left assembly line of operators pulling rows (mermaid of a simple Seek → Nested Loops → Select flow); estimated vs actual = blueprint vs as-built. Concepts with output: reading operators (reuse/point to cheatsheet 06 ranking); STATISTICS IO/TIME anatomy — annotate one real output block line by line; SARGability rules with the fix table (function-on-column → range, implicit conversion → matching type, leading wildcard → can't seek); parameter sniffing mechanics and the three fixes with trade-offs (RECOMPILE / OPTIMIZE FOR / local-variable+its cost). For sqlcmd context: SET STATISTICS XML ON or SET SHOWPLAN_ALL as the non-SSMS way to see plans — show one trimmed plan fragment. Pitfalls demoed: trusting estimated plans when estimates are off; measuring with warm vs cold cache; "missing index" hint over-trust.

**Challenges:**

| # | Text | Expected columns | Hint |
|---|------|------------------|------|
| 1 | SARGability rescue: three slow predicates over Sales.SalesOrderHeader — (a) YEAR(OrderDate) = 2013, (b) CAST(SalesOrderNumber AS VARCHAR) = 'SO43659' (implicit-conversion stand-in), (c) LIKE '%659' — rewrite the two fixable ones to seek, capture logical reads before/after in comments, and explain why (c) fundamentally cannot seek. | (rewrites + evidence) | (a) half-open date range; (b) compare with the right type/N prefix. |
| 2 | Parameter sniffing lab on lesson13.SkewedOrders: a proc filtered by CustomerID; run first for the 90% customer then the tiny one, capture STATISTICS IO for both; then apply each of the three fixes and record how each changes the reads. Summarise the trade-offs in a closing comment. | (proc + evidence per fix) | DBCC FREEPROCCACHE (dev-only!) between runs to force recompiles — note the caveat. |
| 3 | Rewrite race: the same question (per-customer latest-order TotalDue) answered by (a) correlated scalar subquery, (b) JOIN to a grouped derived table, (c) ROW_NUMBER window — capture STATISTICS IO for all three and rank them in the comment, noting the result is workload-dependent, not a universal law. | CustomerID, LatestOrderTotal ×3 variants | — |
| 4 | Join-strategy observation: aggregate SalesOrderDetail by ProductID joined to Product — once naturally (expect Hash Match), once with OPTION (LOOP JOIN) — capture STATISTICS TIME/IO for both, and state in the comment why the optimizer was right (or not) at this row count. | (two runs + evidence) | Hints are for diagnosis here, not production fixes. |

- [ ] Steps 1–6 (setup ×2; evidence from real runs; commit `feat: enhance lesson 13 - deep README + challenge exercises`).

---

### Task 15: Lesson 14 — storage internals

**Files:** `lessons/14-storage-internals/` — README.md, exercises.sql, exercises-solutions.sql, setup.sql (challenges need scratch tables — check existing lesson14 setup, extend idempotently as needed).

**README directives:** Hook: every query you've tuned so far ultimately becomes "how many 8KB pages did we touch" — this lesson opens the box. Mental model: ASCII diagram of an 8KB page (96-byte header / rows growing downward / slot array growing upward from the foot), and the heap-vs-B-tree row-lookup contrast (RID vs key traversal). Concepts with output: page anatomy with a real trimmed DBCC PAGE dump annotated; extents and allocation (sys.dm_db_database_page_allocations trimmed output); finding a row's physical address with sys.fn_PhysLocFormatter; page splits (before/after page counts); forwarded records in heaps (DMV forwarded_record_count shown). All DBCC PAGE runs need TRACEON(3604) — show it once, prominently. Pitfalls: DBCC PAGE is undocumented/dev-only; page-count intuition breaking on wide rows; heap rebuild resets forwarding.

**Challenges (investigation-style):**

| # | Text | Expected columns | Hint |
|---|------|------------------|------|
| 1 | Page treasure hunt: insert a recognisable row ('FIND-ME-42') into a lesson14 table, locate its exact file:page:slot with sys.fn_PhysLocFormatter, then dump that page with DBCC PAGE (..., 3) and identify your row's bytes in the output. Paste the located slot section (trimmed) into the solution comment. | (narrated hunt) | TRACEON(3604) first. |
| 2 | Page-split lab: two identical tables, one clustered on IDENTITY, one on NEWID(); insert 10,000 rows into each; compare page counts and avg_fragmentation_in_percent from sys.dm_db_index_physical_stats. Explain the ~2× page difference via splits in the comment. | TableName, PageCount, AvgFragPct | — |
| 3 | Forwarded-record lab: heap with a VARCHAR(2000) column, insert 1,000 short rows, UPDATE a third of them to long values, read forwarded_record_count from dm_db_index_physical_stats DETAILED mode, then ALTER TABLE ... REBUILD and show the count return to 0. | ForwardedBefore, ForwardedAfter | Updates that grow heap rows leave forwarding pointers. |
| 4 | Rows-per-page arithmetic: for a fixed-width lesson14 table, predict rows-per-page from column widths (+ header/slot overhead) in a comment, then verify against dm_db_index_physical_stats avg record size and page count. Within ±10% passes. | PredictedRowsPerPage, ActualRowsPerPage | 8096 usable bytes; each row costs width + ~7 bytes overhead + 2-byte slot entry. |

- [ ] Steps 1–6 (commit `feat: enhance lesson 14 - deep README + challenge exercises`).

---

### Task 16: Lesson 15 — query lifecycle and optimizer

**Files:** `lessons/15-query-lifecycle-optimizer/` — README.md, exercises.sql, exercises-solutions.sql, setup.sql (extend idempotently for the skew/stale-stats labs as needed).

**README directives:** Hook: the optimizer never sees your data — only statistics about it; when the statistics lie, plans go bad at 3am. Mental model: mermaid pipeline parse → bind → optimize → execute, with the plan cache as a bypass lane around optimize, and stats feeding the optimize box. Concepts with output: the four phases (show a binding error vs syntax error to prove the phases exist); statistics objects and DBCC SHOW_STATISTICS — annotate a real trimmed histogram (RANGE_HI_KEY, EQ_ROWS, RANGE_ROWS); cardinality estimation — predict-then-check one range query's estimate; the plan cache DMVs (top queries by reads); estimate-vs-actual divergence and auto-update thresholds. Pitfalls: stats sampled vs FULLSCAN differences; local variables defeating the histogram (density instead); DBCC FREEPROCCACHE in prod.

**Challenges:**

| # | Text | Expected columns | Hint |
|---|------|------------------|------|
| 1 | Histogram fortune-telling: with a stats object on a lesson15 table, read DBCC SHOW_STATISTICS and PREDICT (in a comment, before running) the estimated rows for one equality on a RANGE_HI_KEY value and one range straddling two steps; then run with actual-plan/STATISTICS output and compare predicted vs estimated vs actual. | (prediction + verification narrative) | EQ_ROWS for the hit; sum RANGE_ROWS+EQ_ROWS across straddled steps. |
| 2 | Stale-stats crime scene: load lesson15 table with 1k rows, ensure stats exist, bulk-insert 100k skewed rows with AUTO_UPDATE off (or below threshold), show a query with wildly wrong estimates; UPDATE STATISTICS WITH FULLSCAN; show estimates repaired. Record estimated-vs-actual rows at each stage in comments. | (evidence trail) | — |
| 3 | Plan-cache census: query sys.dm_exec_query_stats + dm_exec_sql_text for the top 5 statements by total_logical_reads, then measure the single-use-plan share of the cache (execution_count = 1) — the ad-hoc-workload smell — from dm_exec_cached_plans. | QueryText, ExecCount, TotalReads / SingleUsePct | — |
| 4 | Variable vs literal estimates: same filter as a literal, a local variable, and a RECOMPILE-hinted variable query — capture the estimated rows for each and explain in the comment which part of the stats object each read (histogram / density vector / histogram again). | (three estimates + explanation) | — |

- [ ] Steps 1–6 (commit `feat: enhance lesson 15 - deep README + challenge exercises`).

---

### Task 17: Lesson 16 — memory, buffer pool, log

**Files:** `lessons/16-memory-bufferpool-log/` — README.md, exercises.sql, exercises-solutions.sql, setup.sql (extend idempotently as needed).

**README directives:** Hook: "the database is in RAM; the disk is just where it naps" — and the log is why crashing loses nothing. Mental model: mermaid of the write path — change made to page in buffer pool (dirty) → log record forced to disk at commit (WAL) → checkpoint later flushes the page; the reader path beside it (cache hit vs physical read). Concepts with output: buffer-pool census via sys.dm_os_buffer_descriptors (which tables occupy cache — real trimmed output); dirty vs clean, checkpoint, lazy writer; PLE from dm_os_performance_counters; WAL + log anatomy, VLFs via sys.dm_db_log_info; log_reuse_wait_desc; memory grants and tempdb spills (grant warning in plan). Pitfalls: SHRINKFILE on the log as a habit; autogrowth VLF explosions; equating PLE dips with disaster.

**Challenges (investigation-style):**

| # | Text | Expected columns | Hint |
|---|------|------------------|------|
| 1 | Buffer-pool census: which 10 objects in AdventureWorks2022 hold the most buffer-pool pages right now (join dm_os_buffer_descriptors → allocation_unit → partition → object), with dirty-page counts. Run a big scan, run the census again, show the table climbing the chart. | ObjectName, CachedPages, DirtyPages | — |
| 2 | Watch WAL happen: in one transaction, update 100 lesson16 rows; before COMMIT, read sys.dm_tran_database_transactions for this database's log bytes used; then commit. In comments: why those bytes must hit disk before COMMIT returns, and what CHECKPOINT then does for the data pages. | LogBytesUsed | — |
| 3 | Log-truncation detective: set AdventureWorks2022 log_reuse baseline; open a transaction that stays open, generate log activity from another batch, show log_reuse_wait_desc = ACTIVE_TRANSACTION and dm_db_log_space_usage climbing; commit; CHECKPOINT; show it clear. Two-session pattern, both scripts provided. | (narrated evidence) | SIMPLE recovery: checkpoint truncates; the open txn blocks it anyway. |
| 4 | Spill lab: force a sort spill by sorting a large lesson16 rowset immediately after a small memory grant (e.g. bad estimate via stale stats or a compiled 1-row variable pattern from lesson 15); detect it in sys.dm_exec_query_stats (total_spills) or the actual-plan SpillToTempDb warning; then fix the estimate and show the spill vanish. | (before/after spill evidence) | Reuses lesson 15's variable-estimate trick — challenges may reach backward. |

- [ ] Steps 1–6 (two-session challenge verified per section D pattern; commit `feat: enhance lesson 16 - deep README + challenge exercises`).

---

### Task 18: Lesson 17 — locking, blocking, waits

**Files:** `lessons/17-locking-blocking-waits/` — README.md, exercises.sql, exercises-solutions.sql, setup.sql (extend idempotently — escalation demo needs a ~10k-row table).

**README directives:** Hook: nothing is slow, nothing errors — everything is simply *waiting*; this lesson is how you find out on what. Mental model: ASCII lock-compatibility matrix (S/U/X/IS/IX) plus the granularity pyramid (row→page→table) with escalation at ~5,000 locks; one mermaid of a 3-session blocking chain with the root blocker highlighted. Concepts with output: reading dm_tran_locks (real trimmed output annotated); building a blocking chain query over dm_exec_requests (blocking_session_id) up to the root; deadlock anatomy + reading the system_health XEvent deadlock graph; waits-and-queues via dm_os_wait_stats deltas — the "what is the server waiting on" master lens. Pitfalls: NOLOCK as "go faster" (dirty/duplicate/missed rows — precise, brief); killing the root blocker without knowing what rolls back; cumulative wait stats without a baseline delta.

**Challenges:** (all multi-session; per-session scripts provided in exercises.sql comments, expected observations narrated)

| # | Text | Expected columns | Hint |
|---|------|------------------|------|
| 1 | Deadlock, two ways to cause, two ways to fix: script the classic two-session crossed-order UPDATE deadlock on lesson17 tables (session scripts + timing choreography given), capture the graph from system_health; then fix it (a) by consistent object ordering and (b) with an application-lock/serialised approach (sp_getapplock), proving each fix by rerunning. | (deadlock graph XML fragment + fixed reruns) | Victim gets error 1205; the survivor commits. |
| 2 | Blocking-chain forensics: choreograph 3 sessions — A holds X, B waits on A, C waits on B; from a 4th connection, write ONE query over dm_exec_requests/dm_tran_locks that outputs the chain with a RootBlocker column (recursive CTE over blocking_session_id — lesson 06 pays off). | SessionID, BlockedBy, RootBlocker, WaitResource, WaitMs | — |
| 3 | Lock-escalation threshold hunt: in a transaction, UPDATE increasing row counts (1k / 4k / 6k) over the lesson17 10k-row table, checking dm_tran_locks lock counts and granularity at each; find where row locks collapse into one table X lock; then show ALTER TABLE SET (LOCK_ESCALATION = DISABLE) changing the behaviour (and note the memory trade-off). | RowsUpdated, LockCount, MaxGranularity | Escalation attempts at ~5,000 locks per statement. |
| 4 | Wait-stats delta lab: snapshot dm_os_wait_stats into a temp table, run a mixed workload (a big scan + a blocked update from another session for ~5s), snapshot again, and produce the top-10 waits delta excluding benign waits (a provided ignore-list of QUEUE/SLEEP/BROKER types). Interpret the top 2 in a comment. | WaitType, DeltaWaitMs, DeltaCount | — |

- [ ] Steps 1–6. Multi-session choreography verified with parallel `docker exec` sqlcmd sessions (background `&` for holders, foreground for observers; WAITFOR DELAY to sequence). Commit `feat: enhance lesson 17 - deep README + challenge exercises`.

---

### Task 19: Lesson 18 — capstone (lightest touch)

**Files:** `lessons/18-complex-queries/` — README.md (small polish only), exercises-solutions.sql

Per spec: lesson 18 is already the exemplar. Scope here is strictly:

- [ ] **Step 1: Upgrade the 8 existing solution comments** in exercises-solutions.sql from the 1–2-line convention to the reasoning-path style (3–6 lines: decomposition, technique choice, tempting-wrong-approach) — the capstone's solutions should model the same standard the new challenge solutions set. Do not change any query.
- [ ] **Step 2: README polish only:** align its Exercises section wording with the new curriculum-wide framing (challenges elsewhere point forward to lesson 18 as the destination); fix anything stale. No structural rework.
- [ ] **Step 3: Verify** — run lesson 18 setup.sql ×2 and exercises-solutions.sql; identical results to before (comments only).
- [ ] **Step 4: Commit** — `feat: enhance lesson 18 - reasoning-path solution comments`

---

### Task 20: Spec addendum + final sweep

**Files:**
- Modify: `docs/superpowers/specs/2026-05-29-mssql-learning-design.md` (append addendum section at end)

- [ ] **Step 1: Append the addendum** to the master design spec:

```markdown
## Addendum (2026-07-11): Challenge exercises and README depth

Adopted curriculum-wide by the lesson-enhancement work
(see 2026-07-11-lesson-enhancement-design.md). New conventions,
binding for future lessons:

- `exercises.sql` ends with a `-- CHALLENGES --` banner section:
  2–4 harder problems that combine the lesson's topic with earlier
  lessons. Existing exercise numbering is never disturbed.
- Challenge solutions carry a 3–6 line reasoning-path comment
  (decomposition, technique choice and why, the tempting-but-wrong
  approach). Regular solutions keep the 1–2 line convention.
- READMEs follow the deep template: motivating hook, mental-model
  section, stepwise concepts with real captured output, pitfalls
  with "see it break" demos. Target 150–250 lines.
```

- [ ] **Step 2: Final sweep** — `grep -rL "CHALLENGES" lessons/*/exercises.sql` must return only nothing or `18-complex-queries` (its exercises already exceed the bar; challenges there were out of scope). `git status` clean apart from the addendum. All lessons committed.
- [ ] **Step 3: Commit** — `docs: document challenge-exercise and deep-README conventions in master spec`
- [ ] **Step 4: Report completion to the user** with a summary: lessons touched, challenge count added, any deviations from plan.

---

## Execution notes

- **Task order:** 1 → 2 (pilot, HARD STOP for review) → 3–18 in numeric lesson order → 19 → 20. Tasks 3–18 are independent of each other but sequential execution keeps container state simple.
- **Session lifetime:** the `SA_PASSWORD` variable and `runsql` helper must be re-established in every new shell (section A).
- **If a challenge proves unverifiable as specced** (data shape assumption wrong, feature unavailable in the container), adapt the challenge to nearby data that preserves the *technique being exercised*, note the deviation in the final report — don't silently drop it.
- **Instance-level side effects** (ALTER DATABASE snapshot settings, LOCK_ESCALATION, trace flags): every script that sets one must revert it at the end, and setup.sql idempotency must not depend on it.
