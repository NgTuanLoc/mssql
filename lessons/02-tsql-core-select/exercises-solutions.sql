USE AdventureWorks2022;
GO

-- Exercise 1: Employees whose job title contains 'Manager'.
-- Approach: LIKE with % on both sides matches anywhere in the string.
SELECT BusinessEntityID, JobTitle
FROM HumanResources.Employee
WHERE JobTitle LIKE '%Manager%'
ORDER BY JobTitle;

-- Exercise 2: Products in subcategories 1–3 with ListPrice > $100.
-- Approach: IN for the subcategory set, AND for the price filter.
SELECT Name, ProductSubcategoryID, ListPrice
FROM Production.Product
WHERE ProductSubcategoryID IN (1, 2, 3)
  AND ListPrice > 100
ORDER BY ListPrice DESC;

-- Exercise 3: Orders in Q1 2013.
-- Approach: BETWEEN is inclusive; use DATE literals for clarity.
SELECT SalesOrderID, OrderDate, TotalDue
FROM Sales.SalesOrderHeader
WHERE OrderDate BETWEEN '2013-01-01' AND '2013-03-31'
ORDER BY OrderDate;

-- Exercise 4: Products with neither size nor weight.
-- Approach: two IS NULL conditions joined by AND.
SELECT ProductID, Name
FROM Production.Product
WHERE Size IS NULL
  AND Weight IS NULL;

-- Exercise 5: Products 21–30 by name (zero-based offset = 20).
-- Approach: OFFSET n ROWS skips n rows; FETCH NEXT m ROWS ONLY takes m.
SELECT ProductID, Name
FROM Production.Product
ORDER BY Name
OFFSET 20 ROWS
FETCH NEXT 10 ROWS ONLY;


------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: Page 3 of a product listing, 10 rows/page, fully deterministic order.
-- Reasoning: OFFSET/FETCH handles the paging math directly — 10/page means OFFSET 20 for
--   page 3. Determinism needs a second ORDER BY key because 56 ListPrice values have ties
--   (lesson concept 3); ProductID is unique, so appending it breaks every tie without
--   changing the intended price ordering. Tempting-but-wrong: ORDER BY ListPrice DESC
--   alone — it "works" today, but a re-plan or index change can reorder tied rows between
--   pages with no error, silently shifting which products land on which page.
SELECT ProductID, Name, ListPrice
FROM Production.Product
WHERE ListPrice > 0
ORDER BY ListPrice DESC, ProductID
OFFSET 20 ROWS FETCH NEXT 10 ROWS ONLY;

-- Challenge 2: Products that are NOT red, including products with unknown (NULL) color.
-- Reasoning: "Not red" must restore the NULL-color rows a naive `<> 'Red'` drops, so OR in
--   `Color IS NULL` explicitly. Tempting-but-wrong: `Color <> 'Red'` alone — comparing NULL
--   to 'Red' evaluates UNKNOWN, and WHERE discards UNKNOWN exactly like FALSE, silently
--   losing the 248 NULL-color rows (466 correct vs. 218 naive). NOT LIKE/NOT IN carry the
--   same trap for the same reason: any predicate against NULL resolves to UNKNOWN, not
--   FALSE, and WHERE can't tell "unknown" from "no."
SELECT ProductID, Name, Color
FROM Production.Product
WHERE Color <> 'Red' OR Color IS NULL
ORDER BY ProductID;
-- Naive `WHERE Color <> 'Red'` alone returns 218 rows, not 466, for exactly this reason.

-- Challenge 3: Product names containing a literal % or _; AdventureWorks has none, so the
-- seed check's hyphen-then-digit fallback is what actually returns rows.
-- Reasoning: ESCAPE redefines a chosen character (here `!`) so the wildcard right after it
--   is literal, not special — `LIKE '%!%%' ESCAPE '!'` looks for a literal `%`. Verified
--   separately that this dataset has zero name matches for `%` or `_`, so the fallback
--   (`LIKE '%-[0-9]%'`) is what actually returns rows here. Tempting-but-wrong: `LIKE
--   '%%%'` with no ESCAPE — `%` still means "any string" without it; ESCAPE is what makes
--   the character literal, not the choice of `!` itself.
SELECT ProductID, Name
FROM Production.Product
WHERE Name LIKE '%!%%' ESCAPE '!' OR Name LIKE '%!_%' ESCAPE '!';  -- 0 rows in this seed
SELECT ProductID, Name
FROM Production.Product
WHERE Name LIKE '%-[0-9]%'
ORDER BY ProductID;

-- Challenge 4: Orders from the last 30 days of available data, half-open range.
-- Reasoning: "Last 30 days of available data" must anchor to MAX(OrderDate), not today's
--   date — a variable holds that scalar subquery result once. A 30-day window ending on
--   the max day is [MAX−29, MAX+1) half-open: subtracting 30 would span 31 calendar days.
--   The `< end` upper bound sidesteps BETWEEN's inclusive-midnight trap from this lesson's
--   Pitfalls. Tempting-but-wrong: `BETWEEN DATEADD(DAY,-30,@Max) AND @Max` — 31 days wide
--   AND silently drops any order timestamped after midnight on the very last day.
DECLARE @MaxOrderDate DATETIME = (SELECT MAX(OrderDate) FROM Sales.SalesOrderHeader);
DECLARE @StartDate DATETIME = DATEADD(DAY, -29, @MaxOrderDate);
DECLARE @EndDate DATETIME = DATEADD(DAY, 1, @MaxOrderDate);

SELECT SalesOrderID, OrderDate, TotalDue
FROM Sales.SalesOrderHeader
WHERE OrderDate >= @StartDate AND OrderDate < @EndDate
ORDER BY OrderDate, SalesOrderID;
