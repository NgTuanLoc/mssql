# T-SQL Syntax Cheatsheet

Quick reference for T-SQL statement structure, built-in functions, variables, and control flow.

---

## Statement Skeletons

```sql
-- SELECT
SELECT [TOP n] col1, col2, expr AS alias
FROM   schema.table  [WITH (NOLOCK)]
[JOIN  other ON condition]
[WHERE predicate]
[GROUP BY col1]
[HAVING aggregate_condition]
[ORDER BY col1 [ASC|DESC]]
[OFFSET n ROWS FETCH NEXT m ROWS ONLY];

-- INSERT
INSERT INTO schema.table (col1, col2)
VALUES (v1, v2), (v3, v4);

INSERT INTO schema.table (col1, col2)
SELECT col1, col2 FROM other_table WHERE condition;

-- UPDATE
UPDATE t
SET    t.col1 = expr
FROM   schema.table AS t
[JOIN  other AS o ON condition]
WHERE  predicate;

-- DELETE
DELETE t
FROM   schema.table AS t
[JOIN  other AS o ON condition]
WHERE  predicate;

-- MERGE
MERGE  target AS t
USING  source AS s ON (t.id = s.id)
WHEN MATCHED                     THEN UPDATE SET t.col = s.col
WHEN NOT MATCHED BY TARGET       THEN INSERT (col) VALUES (s.col)
WHEN NOT MATCHED BY SOURCE       THEN DELETE;
```

---

## Variables

```sql
DECLARE @name      NVARCHAR(100) = N'Alice';
DECLARE @count     INT;
SET     @count     = 0;

SELECT @count = COUNT(*) FROM dbo.Orders WHERE CustomerID = 1;
PRINT  CAST(@count AS NVARCHAR) + ' orders found';
```

---

## Control Flow

```sql
-- IF / ELSE
IF @count > 10
    PRINT 'High volume';
ELSE IF @count > 0
    PRINT 'Some orders';
ELSE
    PRINT 'No orders';

-- WHILE
WHILE @count < 5
BEGIN
    SET @count += 1;
    PRINT @count;
END;

-- CASE (expression)
SELECT
    CASE Status
        WHEN 1 THEN 'Active'
        WHEN 2 THEN 'Inactive'
        ELSE        'Unknown'
    END AS StatusLabel
FROM dbo.Orders;

-- CASE (searched)
SELECT
    CASE
        WHEN TotalDue > 1000 THEN 'High'
        WHEN TotalDue > 100  THEN 'Medium'
        ELSE                      'Low'
    END AS Tier
FROM Sales.SalesOrderHeader;
```

---

## Batches and GO

`GO` is **not** a T-SQL keyword — it is an SSMS/sqlcmd batch separator. Statements before each `GO` are sent as one batch.

```sql
CREATE TABLE dbo.Foo (id INT);
GO                                -- must separate DDL from DML in same script

INSERT dbo.Foo VALUES (1);
GO
```

---

## String Functions

| Function | Example | Result |
|---|---|---|
| `LEN(s)` | `LEN('hello')` | `5` |
| `DATALENGTH(s)` | `DATALENGTH(N'hi')` | `4` (bytes, 2 per NCHAR) |
| `LEFT(s,n)` | `LEFT('hello',3)` | `'hel'` |
| `RIGHT(s,n)` | `RIGHT('hello',3)` | `'llo'` |
| `SUBSTRING(s,start,len)` | `SUBSTRING('hello',2,3)` | `'ell'` (1-based) |
| `CHARINDEX(find,s)` | `CHARINDEX('l','hello')` | `3` |
| `REPLACE(s,old,new)` | `REPLACE('hello','l','r')` | `'herro'` |
| `LTRIM/RTRIM/TRIM(s)` | `TRIM('  hi  ')` | `'hi'` |
| `UPPER/LOWER(s)` | `LOWER('ABC')` | `'abc'` |
| `CONCAT(a,b,...)` | `CONCAT('a','-','b')` | `'a-b'` |
| `STRING_AGG(col,sep)` | `STRING_AGG(Name,',')` | `'a,b,c'` |
| `FORMAT(val,fmt)` | `FORMAT(1234.5,'N2')` | `'1,234.50'` |

---

## Date Functions

| Function | Notes |
|---|---|
| `GETDATE()` | Current local datetime (DATETIME) |
| `SYSDATETIME()` | Current local datetime (DATETIME2, higher precision) |
| `GETUTCDATE()` | UTC datetime (DATETIME) |
| `DATEADD(part,n,date)` | Add `n` units of `part` to `date` |
| `DATEDIFF(part,start,end)` | Difference in `part` units |
| `DATEPART(part,date)` | Extract integer part (year, month, day, hour…) |
| `DATENAME(part,date)` | Extract named part ('January', 'Monday'…) |
| `EOMONTH(date)` | Last day of the month |
| `FORMAT(date,'yyyy-MM-dd')` | Format as string |
| `CAST(s AS DATE)` | Parse string to date |

Common `part` values: `year`, `quarter`, `month`, `day`, `hour`, `minute`, `second`, `millisecond`, `weekday`.

---

## Math Functions

| Function | Notes |
|---|---|
| `ROUND(n,d)` | Round to `d` decimal places |
| `FLOOR(n)` / `CEILING(n)` | Round down / up to integer |
| `ABS(n)` | Absolute value |
| `POWER(base,exp)` | Exponentiation |
| `SQRT(n)` | Square root |
| `RAND()` | Random FLOAT between 0 and 1 |

---

## Conversion Functions

```sql
CAST(expr AS type)           -- standard SQL, preferred for simple casts
CONVERT(type, expr [, style])-- SQL Server extension; style matters for dates
TRY_CAST(expr AS type)       -- returns NULL on failure instead of error
TRY_CONVERT(type, expr)      -- same, CONVERT variant

-- Examples
SELECT CAST(3.7 AS INT)                        -- 3  (truncates)
SELECT CONVERT(VARCHAR(10), GETDATE(), 120)    -- '2026-05-29' (style 120 = ISO)
SELECT TRY_CAST('abc' AS INT)                  -- NULL (no error)
```

---

## NULL Handling

```sql
ISNULL(expr, replacement)         -- replace NULL with replacement
COALESCE(a, b, c)                 -- first non-NULL; ANSI standard, preferred
NULLIF(a, b)                      -- returns NULL if a = b, else a

-- NULL comparisons: always use IS NULL / IS NOT NULL
WHERE col IS NULL
WHERE col IS NOT NULL
-- col = NULL never matches, even when col is NULL
```

---

## Common Mistakes

- `SELECT *` in production — always name columns explicitly.
- Implicit `NVARCHAR`/`VARCHAR` conversion: comparing `VARCHAR` column to `N'literal'` forces a scan.
- `PRINT` only outputs after the batch completes in SSMS — not mid-loop.
- `RAND()` inside a join is evaluated once per query, not per row — use `NEWID()` for per-row randomness.
- `DATEDIFF(day, start, end)` counts day boundaries crossed, not 24-hour periods.
