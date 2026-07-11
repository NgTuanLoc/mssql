# Lesson 09 — DDL & Constraints

**Prerequisites:** Lessons 01–02 (SELECT basics, data types) and lesson 08 (DML) — every
demo below inserts into the tables it creates.

Bad data is a thousand times more expensive to remove than to reject at the door. An
application bug, a bulk import, or a script run by hand at 2am can all insert garbage if
the only thing stopping them is "the app validates that." Constraints are the last line
of defence no application code can bypass — the engine checks them on every write, from
every client, forever, whether anyone remembers they exist or not.

## What you'll learn

- `CREATE TABLE` with every constraint type: `PRIMARY KEY`, `FOREIGN KEY`, `UNIQUE`,
  `CHECK`, `DEFAULT` — and the exact error each one throws when violated
- `IDENTITY` vs `SEQUENCE` for auto-increment values — and why only one of them can feed
  more than one table
- Computed columns (`AS expression PERSISTED`) and constraining them through their inputs
- `ALTER TABLE` add-column patterns, including the populated-table NOT NULL trap
- `FOREIGN KEY` referential actions: `NO ACTION` (the default) vs `ON DELETE CASCADE`

## The mental model

Every constraint answers one question: *reject this write, or let it through?* Memorize
the toolbox once and every error message below becomes recognizable on sight:

```
CONSTRAINT TOOLBOX
-------------------------------------------------------------------------------------
constraint     rejects                                       checked on      error #
-------------------------------------------------------------------------------------
PRIMARY KEY    duplicate key value; NULL in a key column      INSERT/UPDATE   2627 / 515
UNIQUE         duplicate non-NULL value (multiple NULLs OK)   INSERT/UPDATE   2627
FOREIGN KEY    child row with no matching parent; deleting    INSERT/UPDATE/  547
               a parent that still has children (NO ACTION)   DELETE
CHECK          row where the predicate is FALSE — a NULL      INSERT/UPDATE   547
               operand makes it UNKNOWN, and UNKNOWN passes
NOT NULL       NULL written to the column                     INSERT/UPDATE   515
DEFAULT        (not a rejector) fills a value you omitted     INSERT          —
-------------------------------------------------------------------------------------
```

Every constraint fits one skeleton — inline for single-column rules, named `CONSTRAINT`
clauses for anything you'll reference later (drop, disable, debug):

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

## Setup

Run `setup.sql` once. It creates the empty `lesson09` schema — every table below is
created by the script that uses it, since in a DDL lesson the `CREATE TABLE` *is* the
content.

## Concepts

### 1. `CREATE TABLE` with every constraint type

`Vendor` carries `UNIQUE` and `CHECK`; `PurchaseOrder` adds a `FOREIGN KEY` back to it
plus its own `CHECK` and `DEFAULT`:

```sql
CREATE TABLE lesson09.Vendor (
    VendorID   INT           IDENTITY(1,1) PRIMARY KEY,
    VendorName NVARCHAR(100) NOT NULL,
    TaxNumber  VARCHAR(20)   NOT NULL,
    Rating     TINYINT       NULL,
    CONSTRAINT UQ_Vendor_TaxNumber UNIQUE (TaxNumber),
    CONSTRAINT CK_Vendor_Rating    CHECK (Rating BETWEEN 1 AND 5)
);

CREATE TABLE lesson09.PurchaseOrder (
    PurchaseOrderID INT           IDENTITY(1,1) PRIMARY KEY,
    VendorID        INT           NOT NULL,
    OrderTotal      DECIMAL(12,2) NOT NULL DEFAULT 0,
    CONSTRAINT FK_PurchaseOrder_Vendor FOREIGN KEY (VendorID)
        REFERENCES lesson09.Vendor (VendorID),
    CONSTRAINT CK_PurchaseOrder_Total CHECK (OrderTotal >= 0)
);
-- (insert 2 vendors, 3 orders)
SELECT PurchaseOrderID, VendorID, OrderTotal FROM lesson09.PurchaseOrder ORDER BY PurchaseOrderID;
```

```
PurchaseOrderID VendorID    OrderTotal
--------------- ----------- ----------
              1           1    1500.00
              2           1     320.75
              3           2      89.00
```

Every constraint here is *named* (`UQ_`, `CK_`, `FK_` prefixes) — an unnamed one gets an
unreadable system-generated name instead (see Pitfalls).

**Reach for this when** you're defining a table's shape and want the engine, not the
application layer, to own data integrity.

### 2. `IDENTITY` vs `SEQUENCE` — one counter, two tables

`IDENTITY` is a per-table property: each table's counter starts and advances on its own,
so two `IDENTITY` tables can never share one number line. `SEQUENCE` is a standalone
object — any number of `DEFAULT` clauses can pull from it with `NEXT VALUE FOR`:

```sql
CREATE SEQUENCE lesson09.PaymentSeq AS INT START WITH 5000 INCREMENT BY 1 NO CYCLE;

CREATE TABLE lesson09.WebPayment (
    PaymentID INT PRIMARY KEY DEFAULT (NEXT VALUE FOR lesson09.PaymentSeq),
    Amount    DECIMAL(10,2) NOT NULL
);
CREATE TABLE lesson09.PhonePayment (
    PaymentID INT PRIMARY KEY DEFAULT (NEXT VALUE FOR lesson09.PaymentSeq),
    Amount    DECIMAL(10,2) NOT NULL
);
-- INSERT WebPayment, PhonePayment, WebPayment, PhonePayment (one row each, in that order)
SELECT PaymentID, Amount, 'Web' AS Source FROM lesson09.WebPayment
UNION ALL
SELECT PaymentID, Amount, 'Phone' AS Source FROM lesson09.PhonePayment
ORDER BY PaymentID;
```

```
PaymentID   Amount       Source
----------- ------------ ------
       5000        49.99 Web
       5001       120.00 Phone
       5002        15.50 Web
       5003         7.25 Phone
```

One global, gapless-across-tables order — `5000, 5001, 5002, 5003` — regardless of which
table's `INSERT` pulled the next value. No `IDENTITY` arrangement can do this.

**Reach for this when** unrelated tables need one interleaved numbering scheme (order
numbers, ticket numbers, document numbers issued from more than one entry point).

### 3. Computed columns and `PERSISTED`

A computed column stores an expression, not a value; `PERSISTED` materializes it on disk
so reads don't recompute it every time (writes get slightly slower instead):

```sql
CREATE TABLE lesson09.Timesheet (
    TimesheetID INT          IDENTITY(1,1) PRIMARY KEY,
    HoursWorked DECIMAL(6,2) NOT NULL,
    PayRate     DECIMAL(8,2) NOT NULL,
    GrossPay    AS (HoursWorked * PayRate) PERSISTED
);
-- INSERT (38.5, 22.00), (40.0, 27.50)
SELECT TimesheetID, HoursWorked, PayRate, GrossPay FROM lesson09.Timesheet;
```

```
TimesheetID HoursWorked PayRate    GrossPay
----------- ----------- ---------- ---------
          1       38.50      22.00   847.0000
          2       40.00      27.50  1100.0000
```

`GrossPay` is never written directly — it recomputes from `HoursWorked`/`PayRate` whenever
either changes. Note: `sqlcmd` sessions default to `QUOTED_IDENTIFIER OFF`; any DDL/DML
touching a `PERSISTED` column needs `SET QUOTED_IDENTIFIER ON` first (SSMS sets this for
you) or you'll hit `Msg 1934`.

**Reach for this when** a derived value is read far more often than its inputs change —
line totals, full names, normalized lookups.

### 4. `ALTER TABLE` add-column patterns

Adding a nullable column is always safe; pairing it with a `DEFAULT` fills future rows
without touching existing ones:

```sql
ALTER TABLE lesson09.Vendor ADD Website NVARCHAR(200) NULL;
ALTER TABLE lesson09.Vendor ADD CONSTRAINT DF_Vendor_Website DEFAULT (N'unknown') FOR Website;
-- INSERT a 4th vendor without specifying Website
SELECT VendorID, VendorName, Website FROM lesson09.Vendor ORDER BY VendorID;
```

```
VendorID    VendorName       Website
----------- ---------------- -------
          1 Acme Supplies    NULL
          2 Northwind Parts  NULL
          3 Contoso Ltd      unknown
```

The two pre-existing vendors keep `NULL` — a `DEFAULT` only fires when a row omits the
column, it never backfills existing rows. Only the new vendor gets `unknown`.

**Reach for this when** you're evolving a live schema and can't take the table offline.

### 5. FK referential actions — `NO ACTION` vs `CASCADE`

Two children of `ProjectHeader(ProjectID, ProjectName)`, different actions on the same
kind of FK:

```sql
CREATE TABLE lesson09.ProjectTask (
    TaskID    INT IDENTITY(1,1) PRIMARY KEY,
    ProjectID INT NOT NULL,
    TaskName  NVARCHAR(100) NOT NULL,
    CONSTRAINT FK_ProjectTask_Project FOREIGN KEY (ProjectID)
        REFERENCES lesson09.ProjectHeader (ProjectID) ON DELETE CASCADE
);
CREATE TABLE lesson09.ProjectDocument (
    DocumentID INT IDENTITY(1,1) PRIMARY KEY,
    ProjectID  INT NOT NULL,
    DocName    NVARCHAR(100) NOT NULL,
    CONSTRAINT FK_ProjectDocument_Project FOREIGN KEY (ProjectID)
        REFERENCES lesson09.ProjectHeader (ProjectID)   -- default: NO ACTION
);
-- Project 1 has a ProjectTask child; project 2 has a ProjectDocument child
DELETE FROM lesson09.ProjectHeader WHERE ProjectID = 1;
SELECT * FROM lesson09.ProjectTask;
```

```
TaskID      ProjectID   TaskName
----------- ----------- --------
(0 rows affected)
```

Deleting project 1 succeeded and silently took its tasks along. Deleting project 2 (a
`ProjectDocument` row, no `CASCADE`) instead errors:

```
Msg 547, Level 16, State 1
The DELETE statement conflicted with the REFERENCE constraint "FK_ProjectDocument_Project".
The conflict occurred in database "AdventureWorks2022", table "lesson09.ProjectDocument", column 'ProjectID'.
```

**Reach for this when** child rows have no meaning without the parent (tasks, line items).
Leave `NO ACTION` — the default — when a child (an invoice, an audit record) must survive
or force an explicit decision instead of vanishing silently.

## Worked examples

`examples.sql` runs these against the `lesson09` schema:

1. `CREATE TABLE Customers`/`Orders` with PK, UNIQUE, FK, CHECK, DEFAULT — every constraint named.
2. `INSERT` respecting all of them — notice `CreatedAt`/`Status` fill in from `DEFAULT`.
3. `DBCC CHECKIDENT('lesson09.Customers', NORESEED)` — reports the identity value, doesn't change it.
4. `Invoice` with a `PERSISTED` `LineTotal` — appears in `SELECT *` like any other column.
5. `ALTER TABLE ADD` a nullable column, then a `DEFAULT` — same two-step pattern as concept 4.
6. `sys.tables` filtered to `schema_id = SCHEMA_ID('lesson09')` — how you'd audit schema ownership.
7. A `SEQUENCE` queried directly, two `NEXT VALUE FOR` calls — advances with no table involved.

## Pitfalls

- **Unnamed constraints get unreadable names.** Skip `CONSTRAINT PK_...` and SQL Server
  invents one like `PK__Country__5D9B0D2D9B14C0CE` — you can't guess it, so you can't
  `DROP CONSTRAINT` it without querying `sys.key_constraints` first. Always name them.

- **PK duplicate, FK orphan, CHECK reject — the three you'll see constantly:**

  ```
  Msg 2627, Level 14, State 1
  Violation of PRIMARY KEY constraint 'PK__Country__5D9B0D2D9B14C0CE'. Cannot insert
  duplicate key in object 'lesson09.Country'. The duplicate key value is (US).
  Msg 547, Level 16, State 1
  The INSERT statement conflicted with the FOREIGN KEY constraint "FK_PurchaseOrder_Vendor".
  The conflict occurred in database "AdventureWorks2022", table "lesson09.Vendor", column 'VendorID'.
  Msg 547, Level 16, State 1
  The INSERT statement conflicted with the CHECK constraint "CK_Vendor_Rating". The
  conflict occurred in database "AdventureWorks2022", table "lesson09.Vendor", column 'Rating'.
  ```
  `FOREIGN KEY` and `CHECK` violations share error number 547 — you have to read the
  message text, not just the number, to know which constraint fired. Plain `NOT NULL`
  violations are `Msg 515` instead: `Cannot insert the value NULL into column
  'CountryName' ... column does not allow nulls.`

- **`NULL` slips straight past a `CHECK` constraint.** `CK_Vendor_Rating CHECK (Rating
  BETWEEN 1 AND 5)` looks like it blocks anything outside 1–5 — but `NULL BETWEEN 1 AND 5`
  evaluates to `UNKNOWN`, not `FALSE`, and a `CHECK` only rejects `FALSE`:

  ```sql
  INSERT lesson09.Vendor (VendorName, TaxNumber, Rating) VALUES (N'No Rating Yet', N'TX-9998', NULL);
  ```
  ```
  VendorID    VendorName     Rating
  ----------- -------------- ------
          4 No Rating Yet    NULL
  (1 rows affected)
  ```
  It inserts cleanly. If a column must be rated *and* present, pair the `CHECK` with
  `NOT NULL` — never rely on `CHECK` alone to block missing data.

- **Adding a `NOT NULL` column to a populated table without a `DEFAULT` fails outright:**

  ```sql
  ALTER TABLE lesson09.Country ADD IsActive BIT NOT NULL;
  ```
  ```
  Msg 4901, Level 16, State 1
  ALTER TABLE only allows columns to be added that can contain nulls, or have a DEFAULT
  definition specified, or the column being added is an identity or timestamp column, ...
  Column 'IsActive' cannot be added to non-empty table 'Country' because it does not
  satisfy these conditions.
  ```
  No value exists for the current rows, so the engine refuses outright. Fix — give it a
  `DEFAULT` so existing rows backfill automatically:
  ```sql
  ALTER TABLE lesson09.Country ADD IsActive BIT NOT NULL CONSTRAINT DF_Country_IsActive DEFAULT (1);
  ```
  ```
  CountryCode CountryName    IsActive
  ----------- -------------- --------
  CA          Canada                1
  MX          Mexico                1
  US          United States         1
  ```
  (Challenge 4 explores when even this isn't the best approach for a huge table.)

- **`FOREIGN KEY` actions default to `NO ACTION`** (error on violation, see concept 5) —
  reach for `ON DELETE CASCADE` only when child rows have no meaning without the parent.

- **`IDENTITY` gaps are normal.** A rolled-back transaction still advances the identity
  counter — never assume identity values are contiguous.

- **`UNIQUE` allows multiple `NULL`s.** `NULL <> NULL` in SQL, so a `UNIQUE` column can
  hold any number of `NULL` rows; only one is allowed if the column is also `NOT NULL`.

## Exercises

Open `exercises.sql` and try them in order. Solutions in `exercises-solutions.sql`. The
CHALLENGES section at the bottom is harder: those problems combine this lesson's topic
with earlier lessons and usually need 2+ concepts in one script.

## Cheatsheet link

See `cheatsheets/01-tsql-syntax.md`.
