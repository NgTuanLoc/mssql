# Lesson 12 — Indexes

**Prerequisites:** Lessons 01–05 (SELECT, joins, WHERE) and lesson 08 (execution plans
help but aren't required — this lesson reads plans as it goes).

A dashboard query filters `Sales.SalesOrderHeader`-sized data by a column with no index
and returns a handful of rows. SQL Server can't know in advance which rows match, so it
reads every page of the table checking each one — the table has no way to jump straight
to the answer. An index is that missing shortcut: a separate, sorted structure that lets
the engine jump to the matching rows instead of inspecting all of them.

## What you'll learn

- Clustered vs nonclustered indexes — what they are and when to use each
- Reading `SET STATISTICS IO` to prove a seek happened instead of a scan
- Composite key column order: equality columns lead, range columns trail
- `INCLUDE` columns and eliminating Key Lookups
- Filtered indexes — and when the filter is too weak to matter
- `sys.dm_db_index_usage_stats` and `sys.dm_db_index_physical_stats`

## The mental model

A clustered index **is** the table — its data pages are physically sorted by the key,
so a clustered seek lands directly on the row. A nonclustered index is a second, narrower
structure (like the index cards at the back of a phone book): sorted by its own key, with
each leaf entry pointing back to the base row. If the query needs a column that isn't in
that nonclustered index, the engine flips back to the base table per row — a **Key
Lookup**.

```
NONCLUSTERED INDEX (a card catalog)              CLUSTERED INDEX (the book itself,
sorted by CustomerID, points at pages             physically sorted by SalesOrderID)
                                                  ┌────────────────────────────────┐
        root page                                │ SalesOrderID 43659, ...row...  │ page 401
     [ 11091 | 11500 | ... ]                      │ SalesOrderID 43660, ...row...  │
      /        |         \                       ├────────────────────────────────┤
[..11091]  [11091..11500] [11500..]               │ SalesOrderID 51925, ...row...  │ page 560
   leaf: CustomerID, -> SalesOrderID               │ SalesOrderID 51926, ...row...  │
      |                                           └────────────────────────────────┘
      +---- CustomerID 11091 -> SalesOrderID 51925 -----> flip to page 560 (KEY LOOKUP)
```

Root and intermediate pages narrow the search; leaf pages hold the sorted key plus a
pointer (the clustered key, or a RID on a heap). A seek walks root → intermediate → leaf
in a handful of reads; a scan reads every leaf page in order.

## Setup

Run `setup.sql` once. It creates the `lesson12` schema with four copies of
`SalesOrderHeader`: `SalesOrderHeap` (no index), `SalesOrderCI` (clustered index only),
`SalesOrderNC` (clustered index; nonclustered indexes added as you work through the
concepts below), and `OrderCopy` (clustered index only — a clean table for the
CHALLENGES). Re-run `setup.sql` to reset all indexes and data between attempts.

## Concepts

### 1. Seek vs scan — the read-count proof

Same row, two tables — one with no index, one with a clustered index on `SalesOrderID`:

```sql
SET STATISTICS IO ON;
SELECT SalesOrderID, OrderDate, TotalDue FROM lesson12.SalesOrderHeap WHERE SalesOrderID = 43659;
SELECT SalesOrderID, OrderDate, TotalDue FROM lesson12.SalesOrderCI   WHERE SalesOrderID = 43659;
```

```
Table 'SalesOrderHeap'. Scan count 1, logical reads 866, ...
Table 'SalesOrderCI'.   Scan count 0, logical reads 3, ...
```

866 reads to find one row in the heap (every page checked) vs 3 reads on the clustered
table (root → intermediate → leaf, landing on the row). Same data, same query — the only
difference is a sorted structure to seek into.

**Reach for this when** you want proof a query is seeking, not scanning — read `SELECT`,
change nothing, and diff the logical reads.

### 2. Composite key column order — equality first, range second

An index on `(CustomerID, OrderDate)` only helps a range predicate if `CustomerID` is
pinned by equality first — the B-tree is sorted by `CustomerID` *then* `OrderDate`, so
`OrderDate` alone can't narrow the search:

```sql
CREATE INDEX IX_lesson12_SalesOrderNC_CustDate ON lesson12.SalesOrderNC (CustomerID, OrderDate);
SET STATISTICS IO ON;
SELECT SalesOrderID, OrderDate FROM lesson12.SalesOrderNC
WHERE CustomerID = 11091 AND OrderDate >= '2013-01-01';        -- leading column present
SELECT SalesOrderID, OrderDate FROM lesson12.SalesOrderNC
WHERE OrderDate >= '2013-01-01';                                -- leading column skipped
```

```
Table 'SalesOrderNC'. Scan count 1, logical reads 2, ...    -- Index Seek, 28 rows
Table 'SalesOrderNC'. Scan count 1, logical reads 88, ...   -- Index Scan, 25943 rows
```

Both queries hit the same index. The first seeks (`CustomerID` pins the search); the
second can't seek on `CustomerID` at all, so it scans the whole (narrow) index instead —
44x more reads, and it only stays cheap because the index itself is narrow.

**Reach for this when** designing a composite index: put the column tested with `=` (or
`IN`) first, the column tested with a range (`>`, `BETWEEN`, `<`) after it.

### 3. `INCLUDE` — eliminating the Key Lookup

The index above doesn't carry `TotalDue`, so asking for it forces a Key Lookup per row:

```sql
SET STATISTICS IO ON;
SELECT SalesOrderID, OrderDate, TotalDue FROM lesson12.SalesOrderNC
WHERE CustomerID = 11091 AND OrderDate >= '2013-01-01';   -- TotalDue not in the index
```

```
Table 'SalesOrderNC'. Scan count 1, logical reads 96, ...   -- Index Seek + 28 Key Lookups
```

Rebuild the same index with `INCLUDE (TotalDue)` and the identical query needs no lookup:

```sql
DROP INDEX IX_lesson12_SalesOrderNC_CustDate ON lesson12.SalesOrderNC;
CREATE INDEX IX_lesson12_SalesOrderNC_CustDate
    ON lesson12.SalesOrderNC (CustomerID, OrderDate) INCLUDE (TotalDue);
```

```
Table 'SalesOrderNC'. Scan count 1, logical reads 3, ...    -- pure Index Seek, no lookup
```

96 reads → 3. `INCLUDE` columns ride along in the leaf pages without widening the key —
cheap to add, and they turn a "seek + N lookups" plan into a single covering seek.

**Reach for this when** `STATISTICS IO` or the plan shows a Key Lookup on a query you run
often — add the missing `SELECT` columns as `INCLUDE`, not as key columns. One caveat:
the optimizer won't always take a seek-plus-lookup plan even when a narrower index
exists — for a large enough match set, N lookups cost more than one scan. A 385-row
`TerritoryID` predicate against `lesson12.OrderCopy` (no covering index for the `SELECT`
columns) gets a plain 805-read clustered scan instead, because the estimated
seek-plus-385-lookups plan would cost more.

### 4. Filtered indexes — and where the win comes from

A filtered index only indexes rows matching a predicate, so it's smaller than a full
index over the same column *if the filter excludes most of the table*. Offline
(phone/mail) orders are 3,806 of 31,465 rows (12%) — a real minority:

```sql
SET STATISTICS IO ON;
SELECT SalesOrderID, OrderDate, TotalDue FROM lesson12.SalesOrderNC WHERE OnlineOrderFlag = 0;
```
```
Table 'SalesOrderNC'. Scan count 1, logical reads 802, ...   -- no support at all yet
```
```sql
SET QUOTED_IDENTIFIER ON;   -- required to CREATE and to USE a filtered index
CREATE INDEX IX_lesson12_SalesOrderNC_OfflineOrders
    ON lesson12.SalesOrderNC (OrderDate) INCLUDE (TotalDue) WHERE OnlineOrderFlag = 0;
SELECT SalesOrderID, OrderDate, TotalDue FROM lesson12.SalesOrderNC WHERE OnlineOrderFlag = 0;
```
```
Table 'SalesOrderNC'. Scan count 1, logical reads 15, ...   -- Index Scan of the filtered index
```

802 → 15. `QUOTED_IDENTIFIER` must be `ON` for both the `CREATE INDEX` and every session
that later queries it — with it `OFF` (sqlcmd's default), creation fails outright, and
if any filtered index already exists on the table, plain `CREATE INDEX`/`INSERT`/`ALTER
INDEX` statements against that same table start failing with `Msg 1934` too.

**Reach for this when** a query always filters to a genuinely small, well-known subset
(soft-deletes, one status, one flag value) — not when the subset is most of the table
(see the Worked Examples note below).

### 5. The two DMVs — usage and fragmentation

`sys.dm_db_index_usage_stats` answers "is this index read, or only written?":

```sql
SELECT i.name, s.user_seeks, s.user_scans, s.user_lookups, s.user_updates
FROM sys.indexes i
LEFT JOIN sys.dm_db_index_usage_stats s
       ON s.object_id = i.object_id AND s.index_id = i.index_id AND s.database_id = DB_ID()
WHERE i.object_id = OBJECT_ID('lesson12.SalesOrderNC') AND i.name IS NOT NULL;
```
```
IndexName                            user_seeks  user_scans  user_lookups  user_updates
IX_lesson12_SalesOrderNC_CustDate     1           0           0             0
IX_lesson12_SalesOrderNC_OfflineOrders 0          1           0             0
PK_lesson12_SalesOrderNC              0           1           1             0
```

`sys.dm_db_index_physical_stats` answers "how fragmented, and how big?":

```sql
SELECT i.name, ips.avg_fragmentation_in_percent, ips.page_count
FROM sys.dm_db_index_physical_stats(DB_ID(), OBJECT_ID('lesson12.SalesOrderNC'), NULL, NULL, 'LIMITED') ips
JOIN sys.indexes i ON i.object_id = ips.object_id AND i.index_id = ips.index_id;
```
```
IndexName                             avg_fragmentation_in_percent  page_count
PK_lesson12_SalesOrderNC              3.15... (varies run to run)   792
IX_lesson12_SalesOrderNC_CustDate     0.0                            117
IX_lesson12_SalesOrderNC_OfflineOrders 7.69...                       13
```

**Reach for this when** deciding whether an index earns its write cost (usage stats), or
whether it's time to `REORGANIZE`/`REBUILD` (physical stats — see Pitfalls for when the
answer is "don't bother"). Caveat: `sys.dm_db_index_usage_stats` resets to zero on every
SQL Server restart, so a "0 reads" row right after a restart doesn't yet prove an index
is unused — give it time before trusting it.

Two rules of thumb for what to index in the first place, ahead of any DMV evidence:
foreign-key columns on child tables (joins and cascading deletes both seek on them), and
any column that shows up in a `WHERE` or `JOIN` predicate often enough to matter.

## Worked examples

`examples.sql` runs these against the `lesson12` schema:

1. Heap scan vs clustered index seek — logical read comparison (same shape as Concept 1).
2. Nonclustered index on `CustomerID` — before/after read counts.
3. Eliminate the resulting Key Lookup with `INCLUDE (OrderDate, TotalDue)`.
4. Composite index column order — leading column present vs skipped.
5. Filtered index on `Status = 5` — notice this table ships **every** order as `Status =
   5`, so the filter excludes nothing; unlike Concept 4's 12%-selective example, this one
   demonstrates the syntax but buys no real read reduction (needs `QUOTED_IDENTIFIER ON`
   to even create — see Concept 4).
6. `sys.dm_db_index_physical_stats` — fragmentation report.
7. `sys.dm_db_index_usage_stats` — seek/scan/lookup counts since last restart.

## Pitfalls

- **Non-SARGable predicate.** Wrapping the indexed column in a function hides it from the
  seek logic, even with an index sitting right there:
  ```sql
  CREATE INDEX IX_lesson12_SalesOrderCI_OrderDate ON lesson12.SalesOrderCI (OrderDate) INCLUDE (TotalDue);
  SELECT SalesOrderID, TotalDue FROM lesson12.SalesOrderCI WHERE YEAR(OrderDate) = 2013;                              -- 104 reads, Index Scan
  SELECT SalesOrderID, TotalDue FROM lesson12.SalesOrderCI WHERE OrderDate >= '2013-01-01' AND OrderDate < '2014-01-01'; -- 49 reads, Index Seek
  ```
  Same rows, same index — `YEAR(OrderDate) = 2013` forces a scan (104 reads) because SQL
  Server would have to evaluate `YEAR()` on every row to know which match; the half-open
  range rewrite seeks (49 reads). Lesson 13 covers SARGability in depth.

- **Over-indexing write cost.** Every nonclustered index is another B-tree the engine
  must maintain on every `INSERT`/`UPDATE`/`DELETE`. Same 1,000-row insert, one table with
  no extra indexes and one with 6:
  ```
  OrderCopy   (0 nonclustered indexes): CPU time = 3 ms,  elapsed time = 4 ms.
  SalesOrderNC (6 nonclustered indexes): CPU time = 19 ms, elapsed time = 20 ms.
  ```
  (times vary) — roughly 5x slower with 6 indexes to update instead of 0. Multiply that
  by every write your OLTP workload makes, and unread indexes (see Concept 5) are pure
  cost with no offsetting benefit. Rule of thumb: more than ~5–7 indexes on a hot OLTP
  table is usually a sign of over-indexing.

- **Fragmentation panic.** High `avg_fragmentation_in_percent` looks alarming, but on a
  small index the absolute cost is noise. After the bulk insert above scattered new keys
  through `IX_lesson12_SalesOrderNC_CustDate`:
  ```
  Before REBUILD: 69.4% fragmented, 180 pages -> SELECT COUNT(*) via that index: 182 logical reads
  After  REBUILD: 0.0%  fragmented, 121 pages -> same query: 123 logical reads
  ```
  A real difference (59 reads, ~472 KB) — but on a table this small it's noise next to
  network/app latency. The classic thresholds still apply once an index is big enough to
  matter:
  ```
  < 10%  → ignore
  10–30% → REORGANIZE (online, low-impact)
  > 30%  → REBUILD (faster; ONLINE = ON avoids blocking on Enterprise)
  ```
  but page count gates all of it: Microsoft's own guidance is that below ~1,000 pages,
  don't bother scheduling maintenance over fragmentation percentage alone.

## Exercises

Open `exercises.sql` and try them in order. Re-run `setup.sql` to reset indexes and data.
Solutions in `exercises-solutions.sql`. The CHALLENGES section at the bottom is harder:
those problems combine this lesson's topic with earlier lessons and usually need 2+
concepts in one query.

## Cheatsheet link

See `cheatsheets/05-indexes.md`
