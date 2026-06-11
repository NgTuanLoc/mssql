# Lesson 14 — Storage & Access Internals

> **Prerequisite:** Tiers 1–4, especially lesson 12 (indexes) and 13 (execution plans). This lesson explains the *why* under those topics.

## What you'll learn
- The 8KB page: header, slot array, and rows
- Extents (mixed vs uniform) and how tables are allocated
- Heaps vs clustered B-trees, and how a row is physically located
- Reading real pages with `DBCC PAGE` and the allocation DMVs
- Why page splits, fragmentation, and heap forwarded records happen

## Setup
Run `setup.sql` once. It creates the `lesson14` schema with a heap copy and a clustered copy of `SalesOrderHeader`, plus a small `SplitDemo` table for page-split experiments. Re-run to reset.

## Concepts

### The 8KB page
Everything in SQL Server is stored in 8KB pages. A page has a 96-byte header, then rows growing from the top, and a slot array growing from the bottom that points to each row. Eight contiguous pages form an **extent** (64KB).

### Heap vs clustered index
- A **heap** has no clustered index — rows live wherever they fit, located via a Row ID (file:page:slot).
- A **clustered index** sorts the data pages by the key in a B-tree (root → intermediate → leaf). The leaf level *is* the data.

### How a row is located
`%%physloc%%` exposes a row's physical RID; `sys.fn_PhysLocFormatter` decodes it to `(file:page:slot)`. `sys.dm_db_database_page_allocations` lists every page of an object. `DBCC PAGE` (with trace flag 3604) prints a page's raw contents.

### Why things go wrong
- **Page split:** inserting into a full page forces SQL Server to allocate a new page and move ~half the rows — causing fragmentation and extra logging.
- **Fragmentation:** logical page order drifts from physical order; scans do more random I/O. `sys.dm_db_index_physical_stats` measures it.
- **Forwarded records:** in a *heap*, an updated row that no longer fits leaves a forwarding pointer to its new location — an extra I/O on every read.

## Worked Investigations (lesson14 schema)
1. `%%physloc%%` — the physical address of a row.
2. `sys.dm_db_database_page_allocations` — list a table's pages and types.
3. `DBCC PAGE` — crack open a real data page.
4. `sys.dm_db_index_physical_stats` — fragmentation and page fullness.
5. Heap forwarded records — before/after an in-place update.
6. Page split — before/after a middle-key insert.

## Common issues this explains
- Why heaps can be slow to read after updates (forwarding pointers).
- Why random-key inserts (e.g., GUIDs) fragment a clustered index.
- Why a low fill factor trades space for fewer splits.

## Pitfalls
- `DBCC PAGE` is a learning/diagnostic tool, not a documented production API — don't build on it.
- `'LIMITED'` mode of `dm_db_index_physical_stats` does not report `forwarded_record_count` or page fullness; use `'DETAILED'` (slower) when you need them.
- `%%physloc%%` changes when a row moves — it is not a stable key.

## Cheatsheet link
See `cheatsheets/05-indexes.md`

## Exercises
Open `exercises.sql` and work them in order. Solutions in `exercises-solutions.sql`.
