# Lesson 12 — Indexes

## What you'll learn
- Clustered vs nonclustered indexes — what they are and when to use each
- Creating indexes: basic, composite, with `INCLUDE`, filtered
- How key lookups happen and how to eliminate them
- Index fragmentation and how to fix it
- `sys.dm_db_index_usage_stats` and `sys.dm_db_index_physical_stats`

## Setup
Run `setup.sql` once. It creates the `lesson12` schema with three copies of `SalesOrderHeader`:
- `SalesOrderHeap` — no index (heap)
- `SalesOrderCI` — clustered index only
- `SalesOrderNC` — clustered index + nonclustered indexes added during examples

Re-run `setup.sql` to reset indexes between attempts.

## Concepts

### Clustered index

The table's data pages are physically sorted by the clustered key. One per table. Usually the primary key. An index seek on the clustered key goes directly to the row — no extra lookup.

### Nonclustered index

Separate B-tree with the key column(s) in sorted order; leaf nodes hold the clustered key (or heap RID) to fetch other columns. Up to 999 per table.

**Key lookup:** when the query needs columns not in the nonclustered index, SQL Server looks them up in the clustered index one row at a time. Expensive at scale — eliminate with `INCLUDE` columns.

### Index syntax

```sql
CREATE INDEX IX_Table_Col
    ON schema.Table (Col1 ASC, Col2 DESC)
    INCLUDE (Col3, Col4)
    WHERE ActiveFlag = 1;   -- filtered index
```

### Choosing what to index

1. FK columns on child tables.
2. Columns in `WHERE` and `JOIN` predicates.
3. Leading columns of composite indexes should be the most selective and/or most filtered.
4. Add `INCLUDE` columns for non-selective columns that appear only in `SELECT`.

### Fragmentation

```
< 10%  → ignore
10–30% → REORGANIZE (online, low-impact)
> 30%  → REBUILD (faster; ONLINE = ON avoids blocking on Enterprise)
```

## Worked Examples (lesson12 schema)
1. Heap scan vs clustered index seek — logical read comparison.
2. Nonclustered index on `CustomerID` — before and after logical reads.
3. Eliminate key lookup with `INCLUDE (OrderDate, TotalDue)`.
4. Composite index column order — leading column matters.
5. Filtered index — only shipped orders.
6. `sys.dm_db_index_physical_stats` — fragmentation report.
7. `sys.dm_db_index_usage_stats` — seek/scan/lookup counts since last restart.

## Pitfalls
- **Too many indexes** — every write must update every index. More than ~5–7 indexes on a hot OLTP table is usually a sign of over-indexing.
- **Leading column skipped** — `IX_Table(A, B)` helps `WHERE A = ?` but not `WHERE B = ?` alone.
- **Non-SARGable predicate** — `WHERE YEAR(col) = 2024` prevents a seek even with an index on `col`. Rewrite as `col >= '2024-01-01' AND col < '2025-01-01'`.
- **Forgetting `INCLUDE`** — a seek + key lookup per row can be worse than a scan for large result sets.
- **`sys.dm_db_index_usage_stats` resets on restart** — don't use it immediately after the container restarts.

## Cheatsheet link
See `cheatsheets/04-indexes.md`

## Exercises
Open `exercises.sql` and try them in order. Re-run `setup.sql` to reset indexes. Solutions in `exercises-solutions.sql`.
