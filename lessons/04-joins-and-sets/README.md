# Lesson 04 — Joins & Set Operations

## What you'll learn
- INNER, LEFT, RIGHT, FULL OUTER, and CROSS joins
- Multi-table join chains
- `UNION` vs `UNION ALL`, `INTERSECT`, `EXCEPT`
- Common join mistakes: fan-out, NULL filtering, non-SARGable conditions

## Setup
Run `setup.sql` once. It creates the `lesson04` schema with small `Left` and `Right` tables to demonstrate join types clearly.

## Concepts

### Join types — what they keep

| Join | Rows kept |
|------|-----------|
| INNER | Only rows with a match on both sides |
| LEFT OUTER | All left rows + matched right (NULL if no match) |
| RIGHT OUTER | Matched left (NULL if no match) + all right rows |
| FULL OUTER | All rows from both sides; NULLs where no match |
| CROSS | Every combination (m × n rows) |

### ON vs WHERE in outer joins

```sql
-- LEFT JOIN with filter in WHERE — silently becomes an INNER JOIN
SELECT * FROM A LEFT JOIN B ON A.id = B.id WHERE B.col = 'x';

-- LEFT JOIN with filter in ON — keeps all A rows; B columns are NULL when no match
SELECT * FROM A LEFT JOIN B ON A.id = B.id AND B.col = 'x';
```

Move filters on the *outer* (right) table to the `ON` clause if you want to preserve left rows.

### Set operations

All require the same number of columns with compatible types. Column names come from the first query.

- `UNION ALL` — all rows, including duplicates. Use when duplicates are acceptable or impossible — faster than `UNION`.
- `UNION` — deduplicates the combined result (sorts internally — slower).
- `INTERSECT` — rows present in both results.
- `EXCEPT` — rows in the first result but not the second.

## Worked Examples (AdventureWorks)
1. INNER JOIN on `lesson04.Left`/`Right` — rows 2 and 3 only.
2. LEFT JOIN — row 1 appears with NULL right-side columns.
3. RIGHT JOIN — row 4 appears with NULL left-side columns.
4. FULL JOIN — all four rows, NULLs on the unmatched sides.
5. Five-table join: order → customer → person → order detail → product.
6. `UNION ALL` vs `UNION` on order detail products.
7. `EXCEPT` — products sold but never purchased.
8. `INTERSECT` — products both sold and purchased.

## Pitfalls
- **Accidental fan-out:** joining one-to-many without being aware multiplies rows (e.g., one order → many order lines → aggregates are wrong).
- **WHERE on outer table kills LEFT JOIN:** `WHERE right.col = value` removes the NULL rows, effectively making it an INNER JOIN.
- **UNION vs UNION ALL:** if duplicates are impossible, `UNION ALL` is always faster.
- **Self-join without aliasing:** you must alias both copies of the table or SQL Server errors.

## Cheatsheet link
See `cheatsheets/03-joins-and-sets.md`

## Exercises
Open `exercises.sql` and try them in order. Solutions in `exercises-solutions.sql`.
