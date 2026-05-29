# Indexes Cheatsheet

---

## Index Types

| Type | Description |
|---|---|
| **Clustered** | Physically orders the table's data pages by key. One per table (usually the PK). |
| **Nonclustered** | Separate B-tree structure; leaf nodes hold key + row locator (clustered key or RID). Up to 999 per table. |
| **Unique** | Enforces uniqueness. Can be clustered or nonclustered. |
| **Filtered** | Nonclustered with a `WHERE` clause — smaller, more efficient for sparse columns. |
| **Columnstore** | Column-oriented storage; optimal for analytics/aggregations over large tables. |
| **Full-text** | For `CONTAINS` / `FREETEXT` linguistic searches on character columns. |

---

## Syntax

```sql
-- Basic nonclustered index
CREATE INDEX IX_SalesOrderHeader_OrderDate
    ON Sales.SalesOrderHeader (OrderDate);

-- Composite index (order matters — most selective / most filtered first)
CREATE INDEX IX_SalesOrderHeader_CustomerDate
    ON Sales.SalesOrderHeader (CustomerID, OrderDate);

-- With included columns (avoid key lookups for common SELECT columns)
CREATE INDEX IX_SalesOrderHeader_CustomerDate_Inc
    ON Sales.SalesOrderHeader (CustomerID, OrderDate)
    INCLUDE (TotalDue, Status);

-- Filtered index (only active orders)
CREATE INDEX IX_SalesOrderHeader_Active
    ON Sales.SalesOrderHeader (OrderDate)
    INCLUDE (TotalDue)
    WHERE Status = 5;

-- Unique index
CREATE UNIQUE INDEX UX_Person_Email
    ON Person.EmailAddress (EmailAddress);

-- Drop
DROP INDEX IX_SalesOrderHeader_OrderDate ON Sales.SalesOrderHeader;
```

---

## What to Index

1. **Foreign keys** — always index FK columns unless the table is tiny.
2. **WHERE / JOIN predicates** — columns that appear in `WHERE` or `ON` clauses.
3. **ORDER BY / GROUP BY columns** — can eliminate a sort operator.
4. **INCLUDE columns** — add frequently SELECTed columns to avoid key lookups without bloating the key.
5. **Filtered indexes** — when queries always filter on a subset (e.g., `Status = 'Active'`, `DeletedAt IS NULL`).

---

## Useful DMVs

```sql
-- Index usage stats (resets on service restart)
SELECT
    OBJECT_NAME(i.object_id)          AS TableName,
    i.name                            AS IndexName,
    i.type_desc,
    s.user_seeks,
    s.user_scans,
    s.user_lookups,
    s.user_updates,
    s.last_user_seek
FROM sys.indexes                   AS i
LEFT JOIN sys.dm_db_index_usage_stats AS s
       ON s.object_id = i.object_id
      AND s.index_id  = i.index_id
      AND s.database_id = DB_ID()
WHERE OBJECTPROPERTY(i.object_id, 'IsUserTable') = 1
ORDER BY s.user_seeks DESC;

-- Missing index suggestions (hints only — validate before creating)
SELECT
    mid.statement,
    mig.avg_total_user_cost * mig.avg_user_impact * (mig.user_seeks + mig.user_scans) AS score,
    mid.equality_columns,
    mid.inequality_columns,
    mid.included_columns
FROM sys.dm_db_missing_index_details   AS mid
JOIN sys.dm_db_missing_index_groups    AS mig ON mig.index_handle    = mid.index_handle
JOIN sys.dm_db_missing_index_group_stats AS migs ON migs.group_handle = mig.index_group_handle
ORDER BY score DESC;

-- Fragmentation (run in context of target database)
SELECT
    OBJECT_NAME(ips.object_id)   AS TableName,
    i.name                       AS IndexName,
    ips.avg_fragmentation_in_percent,
    ips.page_count
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') AS ips
JOIN sys.indexes AS i
  ON i.object_id = ips.object_id AND i.index_id = ips.index_id
WHERE ips.avg_fragmentation_in_percent > 10
ORDER BY ips.avg_fragmentation_in_percent DESC;
```

**Fragmentation remediation:**
- < 30% fragmentation → `ALTER INDEX ... REORGANIZE` (online, low impact)
- ≥ 30% fragmentation → `ALTER INDEX ... REBUILD` (faster, briefly locks by default; use `WITH (ONLINE = ON)` on Enterprise to avoid blocking)

---

## Common Mistakes

- **Too many indexes** — every index slows `INSERT`/`UPDATE`/`DELETE`. Index for reads, but measure the write cost.
- **Leading column mismatch** — `IX_Table (A, B)` helps `WHERE A = ?` but not `WHERE B = ?` alone.
- **Key lookups** — if the query needs columns not in the index key, SQL Server does a key lookup per matching row. Add them as `INCLUDE` columns.
- **Over-indexing FKs** — index FKs on the *many* side (child table), not the *one* side (parent).
- **Ignoring fragmentation** — heavily fragmented indexes cause excessive I/O. Check monthly on active tables.
