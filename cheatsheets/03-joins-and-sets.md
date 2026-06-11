# Joins & Set Operations Cheatsheet

---

## Join Types

```
A      B         INNER JOIN        LEFT JOIN         RIGHT JOIN        FULL JOIN
●●●    ●●●       ●●● ∩ ●●●        ●●● + overlap     overlap + ●●●     ●●● ∪ ●●●
```

### INNER JOIN — rows with matches on both sides

```sql
SELECT o.SalesOrderID, c.AccountNumber
FROM   Sales.SalesOrderHeader AS o
INNER JOIN Sales.Customer      AS c ON c.CustomerID = o.CustomerID;
```

### LEFT (OUTER) JOIN — all left rows; NULLs for unmatched right

```sql
SELECT c.CustomerID, o.SalesOrderID   -- o columns are NULL when no order exists
FROM   Sales.Customer          AS c
LEFT JOIN Sales.SalesOrderHeader AS o ON o.CustomerID = c.CustomerID;
```

### RIGHT (OUTER) JOIN — all right rows; NULLs for unmatched left

Equivalent to flipping LEFT JOIN. Rarely needed — prefer LEFT JOIN for readability.

### FULL (OUTER) JOIN — all rows from both sides; NULLs where no match

```sql
SELECT a.ID AS aID, b.ID AS bID
FROM   dbo.TableA AS a
FULL JOIN dbo.TableB AS b ON b.ID = a.ID;
```

### CROSS JOIN — every combination (Cartesian product)

```sql
SELECT c.Color, s.Size
FROM   dbo.Colors AS c
CROSS JOIN dbo.Sizes AS s;
-- n × m rows
```

### Self Join — join a table to itself

```sql
SELECT e.FirstName AS Employee, m.FirstName AS Manager
FROM   HumanResources.Employee AS e
LEFT JOIN HumanResources.Employee AS m ON m.BusinessEntityID = e.ManagerID;
```

---

## Multi-Table Joins

```sql
SELECT soh.SalesOrderID,
       p.FirstName + ' ' + p.LastName AS CustomerName,
       pr.Name AS ProductName,
       sod.OrderQty
FROM   Sales.SalesOrderHeader  AS soh
JOIN   Sales.Customer          AS c   ON c.CustomerID   = soh.CustomerID
JOIN   Person.Person           AS p   ON p.BusinessEntityID = c.PersonID
JOIN   Sales.SalesOrderDetail  AS sod ON sod.SalesOrderID   = soh.SalesOrderID
JOIN   Production.Product      AS pr  ON pr.ProductID        = sod.ProductID;
```

---

## Set Operations

All set operations require matching column count and compatible types. Column names come from the first query.

### UNION vs UNION ALL

```sql
-- UNION: removes duplicates (sorts internally — slower)
SELECT City FROM Person.Address WHERE StateProvinceID = 1
UNION
SELECT City FROM Person.Address WHERE StateProvinceID = 2;

-- UNION ALL: keeps duplicates (faster, use when duplicates are acceptable or impossible)
SELECT ProductID FROM Sales.SalesOrderDetail
UNION ALL
SELECT ProductID FROM Purchasing.PurchaseOrderDetail;
```

### INTERSECT — rows present in both result sets

```sql
SELECT ProductID FROM Sales.SalesOrderDetail
INTERSECT
SELECT ProductID FROM Purchasing.PurchaseOrderDetail;
```

### EXCEPT — rows in first set but not in second

```sql
-- Products that have been sold but never purchased
SELECT ProductID FROM Sales.SalesOrderDetail
EXCEPT
SELECT ProductID FROM Purchasing.PurchaseOrderDetail;
```

---

## Common Join Mistakes

- **Forgetting NULL in outer joins:** `WHERE right.col = value` on a LEFT JOIN turns it into an INNER JOIN — move the filter to the `ON` clause.
- **Accidental fan-out:** joining a one-to-many relationship without being aware multiplies rows.
- **Non-SARGable join condition:** `ON YEAR(o.OrderDate) = YEAR(s.ShipDate)` prevents index seeks.
- **UNION instead of UNION ALL:** when duplicates are impossible (different PKs), the dedup work is wasted.
