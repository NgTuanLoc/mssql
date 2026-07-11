# Lesson 03 — Data Types & Expressions

**Prerequisites:** Lessons 01–02 (`SELECT` anatomy, filtering with `WHERE`).

A reporting query's `WHERE VarcharCode = N'A001'` looks harmless — until the execution
plan quietly drops the index seek it used yesterday. The `N` marks the literal as
`NVARCHAR`, which outranks `VARCHAR` in SQL Server's type precedence, so the *column*
gets implicitly converted on every row, not the literal — the plan even records a
`PlanAffectingConvert` warning naming the exact expression. This lesson is the toolkit
for knowing which side of a comparison converts, before it costs you an index.

## What you'll learn

- Exact numerics (`DECIMAL`) vs approximate numerics (`FLOAT`/`REAL`), and how `DECIMAL`
  arithmetic grows its scale
- `VARCHAR` vs `NVARCHAR`, the `N` prefix, and the 2-bytes-per-char storage rule
- `DATETIME2` vs legacy `DATETIME` precision — the `.003`-style rounding that bites
- `CAST`/`CONVERT` (with style codes) and the `TRY_` variants that return `NULL` instead
  of erroring
- The type-precedence hierarchy — why implicit conversion always converts the
  *lower*-precedence side, killing index seeks when it's the column

## The mental model

SQL Server ranks every data type in a fixed precedence order. When two different types
meet in a comparison, the engine never "meets in the middle" — it converts the entire
*lower*-precedence side to match the higher one, row by row:

```
   higher precedence (this side wins — stays as-is)
   ┌────────────┬────────┬────────┬───────────────┬──────────┬─────────┐
   │ DATETIME2  │  DATE  │ FLOAT  │ DECIMAL/MONEY │ NVARCHAR │ VARCHAR │
   └────────────┴────────┴────────┴───────────────┴──────────┴─────────┘
   lower precedence (this side is ALWAYS implicitly converted)

   VarcharCode = N'A001'   → NVARCHAR beats VARCHAR: VarcharCode converts on
                             every row (CONVERT_IMPLICIT) — a seek plan dies
   VarcharCode = 1         → INT beats VARCHAR too: same fate, same fix
```

Fix: write the literal in the column's own type, so the column stays on the winning side.

## Setup

Run `setup.sql` once. It creates the `lesson03` schema with two tables: `TypeDemo`
(sample rows demonstrating type differences) and `MixedDates` (date strings in mixed
formats, for Challenge 4).

## Concepts

### 1. Exact vs approximate numerics — and `DECIMAL` scale arithmetic

`FLOAT`/`REAL` store an approximation in binary; `DECIMAL(p,s)` stores an exact value,
and its scale (digits after the decimal point) grows with multiplication — two
`DECIMAL(4,2)` values multiplied give a `DECIMAL(9,4)` result, scale `2+2`:

```sql
SELECT
    CAST(0.1 AS FLOAT) + CAST(0.2 AS FLOAT)                 AS FloatSum,
    CAST(0.1 AS DECIMAL(10,2)) + CAST(0.2 AS DECIMAL(10,2)) AS DecimalSum,
    CAST(19.99 AS DECIMAL(4,2)) * CAST(3 AS DECIMAL(4,2))   AS TimesQty;
```

```
FloatSum                 DecimalSum    TimesQty
------------------------ ------------- -----------
     0.30000000000000004           .30     59.9700
```

`FloatSum` shows binary rounding noise; `DecimalSum` is exactly `.30`; `TimesQty` carries
4 decimal digits, not 2 — exact digits from the scale rule, not rounding noise.

**Reach for this when** a calculation must be exact (money, quantities, totals) —
`FLOAT`/`REAL` are for scientific magnitude, never currency.

### 2. `VARCHAR` vs `NVARCHAR` — the `N` prefix and the 2-bytes-per-char rule

`VARCHAR` stores one byte per character; `NVARCHAR` stores two (UTF-16). `DATALENGTH`
proves it directly on `lesson03.TypeDemo`:

```sql
SELECT NVarcharName,
       DATALENGTH(NVarcharName)                      AS NVarcharBytes,
       DATALENGTH(CAST(NVarcharName AS VARCHAR(100))) AS VarcharBytes
FROM lesson03.TypeDemo ORDER BY ID;
```

```
NVarcharName   NVarcharBytes VarcharBytes
-------------- ------------- ------------
Alice                     10            5
Bob                        6            3
Café                       8            4
正確                        4            2
```

Every `NVarcharBytes` value is exactly double the character count — 2 bytes/char, no
exceptions. Always prefix Unicode literals with `N`: `N'café'`; comparing an unprefixed
literal against an `NVARCHAR` column is the implicit-conversion trap from the mental
model.

**Reach for this when** text is user-facing or could contain non-Latin characters — use
`VARCHAR` only for codes/identifiers you fully control (ASCII-only by construction).

### 3. `DATETIME2` vs `DATETIME` — precision and the `.003` rounding

`DATETIME2(n)` stores exactly what you give it, up to its declared scale. Legacy
`DATETIME` rounds to the nearest 1/300 second — increments of `.000`, `.003`, `.007`:

```sql
SELECT
    CAST('2024-01-01 00:00:00.002' AS DATETIME2(3)) AS AsDateTime2,
    CAST('2024-01-01 00:00:00.002' AS DATETIME)     AS AsDateTime;
```

```
AsDateTime2               AsDateTime
------------------------- -----------------------
2024-01-01 00:00:00.002   2024-01-01 00:00:00.003
```

`DATETIME2` keeps `.002` untouched; `DATETIME` silently rounds it up to `.003` — the
value you stored is not the value you get back.

**Reach for this when** you're choosing a datetime column type — default to
`DATETIME2`; only reach for `DATETIME` to match an existing legacy schema.

### 4. `CAST`/`CONVERT` styles and `TRY_` variants

`CONVERT`'s third argument is a style code for date-string formatting (`CAST` has none) —
common codes: `101` = `MM/DD/YYYY`, `103` = `DD/MM/YYYY`, `120` = ISO `YYYY-MM-DD HH:MI:SS`.
`TRY_CAST`/`TRY_CONVERT` return `NULL` on failure instead of erroring — both in one query:

```sql
SELECT
    CONVERT(VARCHAR(10), h.OrderDate, 101)   AS Style101_US,
    CONVERT(VARCHAR(10), h.OrderDate, 120)   AS Style120_ISO,
    v.RawValue, TRY_CAST(v.RawValue AS INT)  AS AsInt
FROM Sales.SalesOrderHeader h
CROSS JOIN (VALUES ('123'), ('45x'), ('7')) AS v(RawValue)
WHERE h.SalesOrderID = 43659;
```

```
Style101_US Style120_ISO RawValue AsInt
----------- ------------ -------- -----------
05/31/2011  2011-05-31   123              123
05/31/2011  2011-05-31   45x             NULL
05/31/2011  2011-05-31   7                  7
```

Same `OrderDate` formats two ways by style; `'45x'` isn't a valid integer, so `TRY_CAST`
returns `NULL` and the query keeps running — plain `CAST` would error and abort the batch.

**Reach for this when** converting data you don't fully trust the shape of (imported
strings, user input) — `TRY_` variants turn a hard error into a filterable `NULL`.

## Worked examples

`examples.sql` runs these against AdventureWorks + `lesson03.TypeDemo`:

1. `CAST(ListPrice AS VARCHAR(20))` — notice `CAST` needs an explicit length or it
   silently truncates.
2. `CONVERT(VARCHAR(10), OrderDate, 120)` — notice the fixed `YYYY-MM-DD` shape
   regardless of server locale.
3. `TRY_CAST(VarcharCode AS INT)` — every row returns `NULL`; none of the seeded codes
   are pure numbers.
4. `WHERE VarcharCode = 1` vs `WHERE VarcharCode = '1'` — the first forces a per-row
   conversion (mental model), the second doesn't.
5. `PriceDecimal / 3` vs `PriceMoney / 3` — `MONEY` truncates to 4 decimals
   mid-calculation while `DECIMAL` keeps going.
6. `DATEDIFF`/`DATEADD`/`EOMONTH` on `BirthDate` — each returns a different type shape
   (`INT` age, `DATE` future date, `DATE` month-end).
7. `N'Café'` vs `'Café'` — this collation happens to preserve `é` even without `N`
   (Pitfalls has the character that doesn't).

## Pitfalls

- **`FLOAT`/`REAL` are not exact.** `0.1 + 0.2 ≠ 0.3` in binary floating point — see
  Concept 1's `FloatSum` = `0.30000000000000004`. Never use for money.

- **`MONEY` rounds every intermediate result to 4 decimal places; `DECIMAL` doesn't.**
  `$10.00 / 3 * 3` should be `$10.00`:
  ```sql
  SELECT
      CAST(10.00 AS MONEY)         / 3 * 3 AS MoneyResult,
      CAST(10.00 AS DECIMAL(19,4)) / 3 * 3 AS DecimalResult;
  ```
  ```
  MoneyResult   DecimalResult
  ------------- -------------
         9.9999      9.999999
  ```
  `MONEY` locks each step to 4 decimals, losing precision immediately; `DECIMAL`'s bare
  expression keeps a wider intermediate scale, so its error is 100x smaller. Split a
  large sum across many rows and `MONEY`'s loss compounds — `DECIMAL(19,4)` is the
  standard fix for financial math.

- **Implicit conversion of an `NVARCHAR` literal against a `VARCHAR` column costs the
  index** — the hook's exact failure, and it shows up as a genuine plan warning:
  ```sql
  SELECT VarcharCode FROM lesson03.TypeDemo WHERE VarcharCode = N'A001';
  ```
  ```
  <Warnings>
    <PlanAffectingConvert ConvertIssue="Seek Plan" Expression=
      "CONVERT_IMPLICIT(nvarchar(20),[AdventureWorks2022].[lesson03]
      .[TypeDemo].[VarcharCode],0)=[@1]" />
  </Warnings>
  ```
  Drop the `N` prefix (`WHERE VarcharCode = 'A001'`) and the warning disappears —
  `VarcharCode` no longer needs converting because both sides are already `VARCHAR`.

- **`CAST` errors on an invalid date string; `TRY_CAST` returns `NULL`.** February has
  no 31st day:
  ```sql
  SELECT CAST('2014-02-31' AS DATE) AS BadDate;
  -- Msg 241, Level 16, State 1: Conversion failed when converting date
  -- and/or time from character string.
  SELECT TRY_CAST('2014-02-31' AS DATE) AS BadDate;   -- NULL, no error
  ```
  Use `TRY_CAST`/`TRY_CONVERT` for any date string you didn't generate yourself.

- **Forgetting the `N` prefix mangles characters outside the column's collation
  codepage** — not every non-ASCII character is at equal risk. `é` survives here because
  `SQL_Latin1_General_CP1_CI_AS` covers Latin-1, but a CJK character has no such luck:
  ```sql
  SELECT NVarcharName, CAST(NVarcharName AS VARCHAR(100)) AS LostAccent
  FROM lesson03.TypeDemo WHERE NVarcharName = N'正確';
  -- LostAccent: '??' — both characters are lost, silently, no error
  ```

- **`CAST(DATETIME AS DATE)` is the right way to strip time** — not `CONVERT(VARCHAR,
  ...)` followed by re-parsing, which is slower and round-trips through a string format.

- **`YEAR(col) = 2024` in a `WHERE` clause is non-SARGable** — wrapping the column in a
  function blocks index seeks; prefer `col >= '2024-01-01' AND col < '2025-01-01'`.

## Exercises

Open `exercises.sql` and try them in order. Solutions in `exercises-solutions.sql`. The
CHALLENGES section at the bottom is harder: those problems combine this lesson's topic
with earlier lessons and usually need 2+ concepts in one query.

## Cheatsheet link

See `cheatsheets/02-data-types.md`.
