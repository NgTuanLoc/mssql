# Lesson 08 — DML: INSERT / UPDATE / DELETE / MERGE

## What you'll learn
- `INSERT` with explicit column lists and `INSERT...SELECT`
- `UPDATE` with `FROM` (joining another table)
- `DELETE` with `FROM`
- `OUTPUT` clause to capture what changed
- `MERGE` for upsert / synchronisation patterns
- `MERGE` pitfalls

## Setup
Run `setup.sql` once. It creates the `lesson08` schema with `ProductStaging`, `InventoryTarget`, and `InventorySource` tables pre-seeded for exercises. Re-run `setup.sql` to reset data to a clean state between exercise attempts.

## Concepts

### INSERT

```sql
-- Explicit columns — required for INSERT...SELECT; recommended always
INSERT dbo.MyTable (Col1, Col2)
VALUES (1, 'a'), (2, 'b');

-- From another table
INSERT dbo.MyTable (Col1, Col2)
SELECT ColA, ColB FROM dbo.Source WHERE condition;
```

### UPDATE with FROM

```sql
UPDATE t
SET    t.Price = s.Price
FROM   dbo.Target AS t
JOIN   dbo.Source AS s ON s.ID = t.ID;
```

### DELETE with FROM

```sql
DELETE t
FROM   dbo.Target AS t
JOIN   dbo.Source AS s ON s.ID = t.ID
WHERE  s.IsExpired = 1;
```

### OUTPUT clause

```sql
INSERT ... OUTPUT inserted.ID INTO @tbl;
UPDATE ... OUTPUT deleted.Price AS OldPrice, inserted.Price AS NewPrice;
DELETE ... OUTPUT deleted.*;
```

`inserted` = the row after the change; `deleted` = the row before.

### MERGE

```sql
MERGE Target AS t
USING Source AS s ON (s.ID = t.ID)
WHEN MATCHED THEN
    UPDATE SET t.Col = s.Col
WHEN NOT MATCHED BY TARGET THEN
    INSERT (ID, Col) VALUES (s.ID, s.Col)
WHEN NOT MATCHED BY SOURCE THEN
    DELETE;
```

## Worked Examples (lesson08 tables)
1. `INSERT` with VALUES list.
2. `INSERT...SELECT` — bulk load from Production.Product.
3. `UPDATE FROM` — sync prices from the source table.
4. `UPDATE` with `OUTPUT` — capture old and new price.
5. `DELETE` with `OUTPUT` — capture deleted rows.
6. `MERGE` full upsert with OUTPUT showing the action.

## Pitfalls
- `MERGE` has a known race condition under concurrent workloads — use a holdlock hint (`WITH (HOLDLOCK)`) or use explicit `IF EXISTS / UPDATE / INSERT` instead for high-concurrency scenarios.
- `UPDATE` without `WHERE` updates every row — always double-check the predicate.
- `INSERT` without an explicit column list breaks when columns are added to the table.
- `OUTPUT INTO` cannot be used with tables that have triggers.
- `DELETE` from a table with FK constraints will fail if child rows exist.

## Cheatsheet link
See `cheatsheets/01-tsql-syntax.md`

## Exercises
Open `exercises.sql` and try them in order. Re-run `setup.sql` to reset data. Solutions in `exercises-solutions.sql`.
