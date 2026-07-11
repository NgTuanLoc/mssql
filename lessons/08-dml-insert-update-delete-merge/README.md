# Lesson 08 — DML: INSERT / UPDATE / DELETE / MERGE

**Prerequisites:** Lessons 01–05 (SELECT, joins, filtering) and lesson 06 (CTEs — you'll
use one for a windowed `DELETE` in the challenges).

A junior engineer runs `UPDATE Orders SET Status = 'Shipped' FROM Orders o JOIN Shipments
s ON s.OrderID = o.OrderID`, expecting one row updated per order. A buggy carrier feed had
duplicated every `Shipments` row, so the `FROM` join fanned out 1:N across the whole
table, and the `UPDATE` hit every row in `Orders` — applying whichever duplicate happened
to win, with no error and no warning. DML statements are not special-cased "just write the
value" commands — they read first, exactly like a `SELECT`, and the join semantics that
can multiply your `SELECT` rows can just as easily multiply (or corrupt) your writes.

## What you'll learn

- `INSERT` with explicit column lists and `INSERT...SELECT`
- `UPDATE` with `FROM` — and why it's nondeterministic when the join is 1:N
- `DELETE` with a join, to remove rows based on another table's state
- `OUTPUT` to capture exactly what changed, without a follow-up `SELECT`
- `MERGE` anatomy — matched/not-matched branches and `$action`
- Three ways DML bites: fan-out, `MERGE` races, and a forgotten `WHERE`

## The mental model

Every DML statement is **read-then-write**: SQL Server builds a row set exactly the way a
`SELECT` would (same joins, same filters, same nondeterminism), then applies inserts,
updates, or deletes to that row set. `UPDATE ... FROM` makes this literal — the `FROM`
clause is a join that *feeds* the update:

```
UPDATE t SET t.Price = s.Price
FROM   Target AS t JOIN Source AS s ON s.ID = t.ID

   Target                Source                 join result (read phase)      write phase
   ------                ------                  ----------------------      -----------
   ID=1 Price=10   ┐      ID=1 Price=99   ┐       ID=1  t.Price=10 s.Price=99  → SET Price=99
   ID=2 Price=20   ┤ join ID=2 Price=88   ┤  ==>   ID=2  t.Price=20 s.Price=88  → SET Price=88
   ID=3 Price=30   ┘      (no match)      ┘        (ID=3 not in join result)   → unchanged
```

If `Source` had **two** rows for `ID=1`, the join result would have two rows for `ID=1`
too — and the write phase applies exactly one of them, picked arbitrarily. SQL Server
guarantees which row wins for nothing; that's the hook's bug, reproduced in Pitfalls.

## Setup

Run `setup.sql` once. It creates the `lesson08` schema with `ProductStaging`,
`InventoryTarget`, and `InventorySource` for the concepts and exercises, plus a
`ProductPrice` / `ProductPriceStage` pair with a `ProductPriceAudit` table, and two empty
scratch tables (`ProductPriceLog`, `PurgeCandidate`) that the challenges seed for
themselves. Re-run `setup.sql` to reset everything to a clean state.

## Concepts

### 1. `INSERT` — explicit VALUES and `INSERT...SELECT`

```sql
INSERT lesson08.ProductStaging (ProductID, Name, ListPrice)
VALUES (9001, N'Demo Insert', 15.00);

INSERT lesson08.ProductStaging (ProductID, Name, ListPrice)
SELECT ProductID, Name, ListPrice
FROM Production.Product
WHERE ProductSubcategoryID = 2 AND ListPrice > 0
  AND ProductID NOT IN (SELECT ProductID FROM lesson08.ProductStaging);

SELECT TOP 5 ProductID, Name, ListPrice FROM lesson08.ProductStaging ORDER BY StagingID DESC;
```

```
ProductID   Name                                               ListPrice
----------- -------------------------------------------------- ---------------------
        999 Road-750 Black, 52                                              539.9900
        998 Road-750 Black, 48                                              539.9900
        997 Road-750 Black, 44                                              539.9900
        977 Road-750 Black, 58                                              539.9900
        976 Road-350-W Yellow, 48                                          1700.9900
```

Both forms need an explicit column list — required for `INSERT...SELECT` (the source and
target column counts must line up positionally) and cheap insurance for `VALUES` too: if
someone adds a column to the table later, `INSERT tbl VALUES (...)` silently breaks or
silently mis-maps, while `INSERT tbl (a, b) VALUES (...)` keeps working or fails loudly.

**Reach for this when** loading rows one-off (`VALUES`) or in bulk from another table or
query result (`SELECT`).

### 2. `UPDATE ... FROM` — and its nondeterminism

```sql
UPDATE t
SET    t.StockQty   = s.StockQty,
       t.LastSyncAt = SYSDATETIME()
FROM   lesson08.InventoryTarget AS t
JOIN   lesson08.InventorySource AS s ON s.ProductID = t.ProductID;

SELECT ProductID, StockQty FROM lesson08.InventoryTarget ORDER BY ProductID;
```

```
ProductID   StockQty
----------- -----------
          1          95
          2         200
          3         310
```

Two rows matched (`ProductID` 1 and 3) and updated; `ProductID` 2 has no row in
`InventorySource`, so the join drops it from the read phase and it's left untouched — this
is a plain 1:1 join, so the result is fully deterministic. The moment `Source` can have
**more than one row per key**, the join produces duplicate rows for that key and the write
phase must pick one — which one is unspecified and can change between runs or plans. See
Pitfalls for that failure mode reproduced.

**Reach for this when** you need to update a table using values looked up from another —
just confirm the join is 1:1 on the update side first.

### 3. `DELETE` with a join

```sql
DELETE s
OUTPUT deleted.ProductID, deleted.StockQty
FROM   lesson08.InventorySource AS s
LEFT JOIN lesson08.InventoryTarget AS t ON t.ProductID = s.ProductID
WHERE  t.ProductID IS NULL;
```

```
ProductID   StockQty
----------- -----------
          4          50
          5           0
```

The `LEFT JOIN ... WHERE t.ProductID IS NULL` pattern finds "orphan" feed rows — source
rows with no matching target yet — and removes just those two. `DELETE` supports the same
`FROM`/join machinery as `UPDATE`; the alias before `FROM` (`s`) tells SQL Server which
side of the join to actually delete from. Note this only works because `InventorySource`
has no foreign-key children — deleting from a table with FK-referencing child rows still
in place fails with a constraint violation regardless of how you filtered it.

**Reach for this when** the rows to remove are defined by their relationship to another
table, not by a simple column filter.

### 4. `OUTPUT` — capture the change without a follow-up SELECT

```sql
DECLARE @Changed TABLE (ProductID INT, OldPrice MONEY, NewPrice MONEY);

UPDATE lesson08.ProductStaging
SET    ListPrice = ListPrice + 5.00
OUTPUT inserted.ProductID, deleted.ListPrice AS OldPrice, inserted.ListPrice AS NewPrice
INTO   @Changed
WHERE  ProductID IN (514, 515, 516);

SELECT * FROM @Changed ORDER BY ProductID;
```

```
ProductID   OldPrice              NewPrice
----------- --------------------- ---------------------
        514              133.3400              138.3400
        515              147.1400              152.1400
        516              196.9200              201.9200
```

`inserted` is the row *after* the change, `deleted` is the row *before* — for `INSERT`
only `inserted` has rows, for `DELETE` only `deleted` does, and `UPDATE` populates both so
you can diff them in the same statement. Routing `OUTPUT` `INTO` a table variable means no
second round-trip to re-read what you just wrote. One restriction: `OUTPUT INTO` cannot
target a table that has triggers defined on it.

**Reach for this when** the caller needs the changed rows (generated keys, before/after
values) back immediately — logging, audit trails, chaining into the next statement.

### 5. `MERGE` anatomy — matched / not matched / `$action`

```sql
MERGE lesson08.InventoryTarget AS t
USING lesson08.InventorySource AS s
   ON s.ProductID = t.ProductID
WHEN MATCHED AND s.StockQty <> t.StockQty THEN
    UPDATE SET t.StockQty = s.StockQty, t.LastSyncAt = SYSDATETIME()
WHEN NOT MATCHED BY TARGET THEN
    INSERT (ProductID, StockQty, LastSyncAt)
    VALUES (s.ProductID, s.StockQty, SYSDATETIME())
WHEN NOT MATCHED BY SOURCE THEN
    DELETE
OUTPUT $action AS Action, COALESCE(inserted.ProductID, deleted.ProductID) AS ProductID;
```

```
Action     ProductID
---------- -----------
UPDATE               1
DELETE               2
UPDATE               3
INSERT               4
INSERT               5
```

One statement, three branches: rows present in both but different get `UPDATE`ed, rows
only in the source get `INSERT`ed, rows only in the target get `DELETE`d. `$action` in the
`OUTPUT` clause tells you which branch fired for each row — essential once you're auditing
a sync instead of just running it.

**Reach for this when** you're reconciling a target table against a full source snapshot
(insert new, update changed, delete missing) in one atomic statement.

## Worked examples

`examples.sql` runs these against `lesson08.ProductStaging` / `InventoryTarget` /
`InventorySource`:

1. `INSERT` with an explicit `VALUES` list — notice the required column list.
2. `INSERT...SELECT` guarded by `NOT IN` — notice it's safe to re-run without duplicating.
3. `UPDATE ... FROM` syncing prices from `Production.Product` — notice every staged row
   updates because the join is 1:1 on `ProductID`.
4. `UPDATE` with `OUTPUT` — notice `deleted.ListPrice`/`inserted.ListPrice` land in one row.
5. `DELETE` with `OUTPUT` — notice the deleted rows print without a separate `SELECT`.
6. `MERGE` full upsert with `OUTPUT` — notice `$action` varies per row in the same result.

## Pitfalls

- **`UPDATE ... FROM` fan-out picks an arbitrary row when the join is 1:N.** Two
  "corrections" arrive for the same key — SQL Server applies one, not both, with no
  guarantee which:

  ```sql
  UPDATE ps
  SET    ps.ListPrice = src.NewPrice
  FROM   lesson08.ProductStaging AS ps
  JOIN   (VALUES (514, 11.11), (514, 22.22)) AS src(ProductID, NewPrice)
         ON src.ProductID = ps.ProductID;

  SELECT ProductID, ListPrice FROM lesson08.ProductStaging WHERE ProductID = 514;
  ```

  ```
  ProductID   ListPrice
  ----------- ---------------------
          514               11.1100
  ```

  This run landed on `11.11`; a different plan or a different day could just as validly
  land on `22.22`. There is no `ORDER BY` on an `UPDATE`, so nothing controls which source
  row wins. Fix: make the source 1:1 before joining (dedupe, or pick a row explicitly with
  `ROW_NUMBER()` first) — never rely on "whichever one lands."

- **`MERGE` has a documented race condition, plus a list of quirks worth memorizing.**
  Under concurrent sessions, two `MERGE`s can both decide "not matched" for the same key
  and both try to `INSERT` it, causing a PK violation or a duplicate — add
  `WITH (HOLDLOCK)` on the target (or serialize access) for concurrent workloads. Beyond
  concurrency, `MERGE` has sharp edges even single-threaded: the source's join column must
  not produce duplicate matches per target row (errors out at runtime, not compile time),
  triggers on the target fire once per `MERGE` statement rather than once per DML type, and
  `WHEN NOT MATCHED BY SOURCE` with no filter will delete *every* target row the source
  doesn't mention — easy to do by accident on a partial extract.

- **Forgetting `WHERE` on `UPDATE`/`DELETE` touches every row.** Demoed inside a
  transaction so nothing outside this block is actually harmed:

  ```sql
  BEGIN TRAN;
  UPDATE lesson08.ProductStaging SET ListPrice = 0;   -- forgot the WHERE
  SELECT COUNT(*) AS RowsNowZero FROM lesson08.ProductStaging WHERE ListPrice = 0;
  ROLLBACK;
  SELECT COUNT(*) AS RowsStillZeroAfterRollback FROM lesson08.ProductStaging WHERE ListPrice = 0;
  ```

  ```
  RowsNowZero
  -----------
           10

  RowsStillZeroAfterRollback
  --------------------------
                           0
  ```

  All 10 seeded rows zeroed out — the intended target was one row. `ROLLBACK` undoes it
  here only because this is a demo; in a real session that `UPDATE` commits (or
  autocommits) unless you're already inside an explicit transaction. Always run the
  `SELECT` version of your predicate first to see what you're about to touch.

## Exercises

Open `exercises.sql` and try them in order. Re-run `setup.sql` to reset data. Solutions in
`exercises-solutions.sql`. The CHALLENGES section at the bottom is harder: those problems
combine this lesson's topic with earlier lessons (CTEs, `NOT EXISTS`) and usually need 2+
concepts in one script.

## Cheatsheet link

See `cheatsheets/01-tsql-syntax.md`.
