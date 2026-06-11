# Data Types Cheatsheet

---

## Exact Numerics

| Type | Storage | Range / Notes |
|---|---|---|
| `BIT` | 1 bit (1 byte if standalone) | 0, 1, or NULL |
| `TINYINT` | 1 byte | 0–255 |
| `SMALLINT` | 2 bytes | –32,768–32,767 |
| `INT` | 4 bytes | –2.1B–2.1B (default integer choice) |
| `BIGINT` | 8 bytes | ±9.2 × 10¹⁸ |
| `DECIMAL(p,s)` / `NUMERIC(p,s)` | 5–17 bytes | Exact; `p` = total digits, `s` = decimal places |
| `MONEY` | 8 bytes | ±922 trillion, 4 decimal places — avoid; use `DECIMAL(19,4)` instead |
| `SMALLMONEY` | 4 bytes | ±214,748, 4 decimal places |

---

## Approximate Numerics

| Type | Storage | Notes |
|---|---|---|
| `FLOAT(n)` | 4 or 8 bytes | `n` 1–24 → 4 bytes; 25–53 → 8 bytes. Not exact — never use for money. |
| `REAL` | 4 bytes | Alias for `FLOAT(24)` |

---

## Strings

| Type | Max length | Notes |
|---|---|---|
| `CHAR(n)` | 8,000 bytes | Fixed-width ASCII. Pads with spaces. |
| `VARCHAR(n)` | 8,000 bytes | Variable-width ASCII. `VARCHAR(MAX)` → 2 GB. |
| `NCHAR(n)` | 4,000 chars | Fixed-width Unicode (UTF-16). |
| `NVARCHAR(n)` | 4,000 chars | Variable-width Unicode. `NVARCHAR(MAX)` → 2 GB. Use for user-facing text. |

**Rule of thumb:** use `NVARCHAR` for anything a user types; `VARCHAR` for codes and identifiers you control.

Literals: prefix Unicode strings with `N`: `N'café'`. Without `N`, non-ASCII characters are silently mangled.

---

## Date and Time

| Type | Accuracy | Range | Storage | Notes |
|---|---|---|---|---|
| `DATE` | 1 day | 0001–9999 | 3 bytes | Date only |
| `TIME(n)` | 100ns (n=7) | 00:00–23:59 | 3–5 bytes | Time only |
| `DATETIME` | ~3.33ms | 1753–9999 | 8 bytes | Legacy; rounding surprises |
| `DATETIME2(n)` | 100ns (n=7) | 0001–9999 | 6–8 bytes | **Preferred** for datetime |
| `SMALLDATETIME` | 1 min | 1900–2079 | 4 bytes | Legacy |
| `DATETIMEOFFSET(n)` | 100ns | 0001–9999 | 8–10 bytes | Includes UTC offset; use for timezone-aware data |

---

## Other Types

| Type | Notes |
|---|---|
| `UNIQUEIDENTIFIER` | 16-byte GUID. `NEWID()` generates a random GUID; `NEWSEQUENTIALID()` generates ordered GUIDs (better for clustered index). |
| `XML` | Up to 2 GB. Has its own query methods (`.value()`, `.nodes()`). |
| `VARBINARY(n)` / `VARBINARY(MAX)` | Binary data. `MAX` = 2 GB. |
| `ROWVERSION` / `TIMESTAMP` | Auto-incrementing binary, used for optimistic concurrency. |

---

## Implicit Conversion Gotchas

```sql
-- WRONG: implicit conversion from INT to VARCHAR forces a full scan on a VARCHAR index
WHERE VarcharColumn = 42          -- 42 is INT; SQL Server must convert every row

-- RIGHT
WHERE VarcharColumn = '42'

-- WRONG: comparing DATE to DATETIME loses index SARGability in some cases
WHERE DatetimeCol = '2024-01-01'  -- '2024-01-01' becomes midnight; misses rows at other times

-- RIGHT for open-ended date ranges
WHERE DatetimeCol >= '2024-01-01' AND DatetimeCol < '2024-01-02'
```

Implicit conversion chart: SQL Server will implicitly convert "lower" types to "higher" types in the data type precedence hierarchy. When in doubt, use explicit `CAST`/`CONVERT`.

---

## Common Mistakes

- Using `FLOAT`/`REAL` for financial data — use `DECIMAL(p,s)`.
- `MONEY` rounding: intermediate arithmetic uses only 4 decimal places, causing errors. Use `DECIMAL(19,4)`.
- `DATETIME` rounds to nearest 3.33ms — never use for precise time storage.
- Forgetting `N` prefix on Unicode literals: `'café'` silently drops the accent on non-Unicode columns.
- `VARCHAR(MAX)` columns cannot be indexed (only first 900 bytes usable in an index key).
