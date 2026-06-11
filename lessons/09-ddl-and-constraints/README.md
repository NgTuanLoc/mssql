# Lesson 09 — DDL & Constraints

## What you'll learn
- `CREATE TABLE` with `PRIMARY KEY`, `FOREIGN KEY`, `UNIQUE`, `CHECK`, `DEFAULT`
- `IDENTITY` vs `SEQUENCE` for auto-increment values
- Computed columns (`AS expression PERSISTED`)
- `ALTER TABLE` to add columns and constraints
- Schema organisation

## Setup
Run `setup.sql` once (creates empty `lesson09` schema).

## Concepts

### CREATE TABLE skeleton

```sql
CREATE TABLE schema.TableName (
    ID       INT           IDENTITY(1,1) PRIMARY KEY,
    Code     VARCHAR(20)   NOT NULL,
    Amount   DECIMAL(10,2) NOT NULL DEFAULT 0,
    ParentID INT           NULL,
    CONSTRAINT UQ_TableName_Code   UNIQUE (Code),
    CONSTRAINT CK_TableName_Amount CHECK (Amount >= 0),
    CONSTRAINT FK_TableName_Parent FOREIGN KEY (ParentID)
        REFERENCES schema.Parent (ID)
);
```

### Constraint types

| Constraint | Enforces |
|---|---|
| `PRIMARY KEY` | Uniqueness + NOT NULL; one per table |
| `UNIQUE` | Uniqueness; NULLs allowed (one NULL per column by default) |
| `FOREIGN KEY` | Referential integrity to another table |
| `CHECK` | Arbitrary boolean condition |
| `DEFAULT` | Value inserted when column is omitted |

### IDENTITY vs SEQUENCE

- `IDENTITY(seed, increment)` — per-table auto-increment. Cannot be shared across tables.
- `SEQUENCE` — standalone object; shareable, gapless options available, can be queried with `NEXT VALUE FOR`.

### Computed columns

```sql
LineTotal AS (Quantity * UnitPrice) PERSISTED
```

`PERSISTED` stores the result on disk — faster reads, slower writes. Non-persisted columns are computed on every read.

## Worked Examples (lesson09 schema)
1. `CREATE TABLE` with PK, UNIQUE, FK, CHECK, DEFAULT.
2. `INSERT` data respecting constraints.
3. `DBCC CHECKIDENT` — inspect IDENTITY seed.
4. Computed column: `LineTotal AS (Quantity * UnitPrice) PERSISTED`.
5. `ALTER TABLE ADD` column and constraint.
6. Explore schema membership via `sys.tables`.
7. `SEQUENCE` object for shared auto-increment.

## Pitfalls
- Always name constraints (`CONSTRAINT CK_...`). Unnamed constraints get system-generated names like `CK__TableName__Amount__3C69FB99` — impossible to reference in `ALTER TABLE DROP CONSTRAINT`.
- `FOREIGN KEY` actions default to `NO ACTION` (error on violation). Consider `ON DELETE CASCADE` only for child records that have no meaning without the parent.
- `IDENTITY` gaps are normal — a rolled-back transaction still advances the identity counter.
- `UNIQUE` allows multiple NULL values (NULL ≠ NULL in SQL); only one per column if the column is `NOT NULL`.

## Cheatsheet link
See `cheatsheets/01-tsql-syntax.md`

## Exercises
Open `exercises.sql` and try them in order. Solutions in `exercises-solutions.sql`.
