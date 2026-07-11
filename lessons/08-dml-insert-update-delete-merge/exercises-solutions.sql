USE AdventureWorks2022;
GO

-- Exercise 1: Insert 3 test rows.
-- Approach: multi-row VALUES list with explicit columns.
INSERT lesson08.ProductStaging (ProductID, Name, ListPrice)
VALUES (8001, N'Test A', 10.00),
       (8002, N'Test B', 20.00),
       (8003, N'Test C', 30.00);

-- Exercise 2: Double prices for 8001+ with OUTPUT.
-- Approach: OUTPUT deleted.ListPrice (before) and inserted.ListPrice (after).
UPDATE lesson08.ProductStaging
SET    ListPrice = ListPrice * 2
OUTPUT inserted.ProductID,
       deleted.ListPrice  AS OldPrice,
       inserted.ListPrice AS NewPrice
WHERE  ProductID >= 8001;

-- Exercise 3: Delete zero-price rows with OUTPUT.
-- Approach: OUTPUT deleted.* captures the rows being removed.
DELETE lesson08.ProductStaging
OUTPUT deleted.ProductID, deleted.Name
WHERE  ListPrice = 0;

-- Exercise 4: MERGE without the DELETE branch.
-- Approach: omit WHEN NOT MATCHED BY SOURCE to leave orphan target rows alone.
MERGE lesson08.InventoryTarget AS t
USING lesson08.InventorySource AS s
   ON s.ProductID = t.ProductID
WHEN MATCHED AND s.StockQty <> t.StockQty THEN
    UPDATE SET t.StockQty = s.StockQty, t.LastSyncAt = SYSDATETIME()
WHEN NOT MATCHED BY TARGET THEN
    INSERT (ProductID, StockQty, LastSyncAt)
    VALUES (s.ProductID, s.StockQty, SYSDATETIME());

-- Exercise 5: INSERT...SELECT into table variable with OUTPUT.
-- Approach: declare the table variable first; use OUTPUT INTO to fill it.
DECLARE @Copied TABLE (ProductID INT, Name NVARCHAR(50), ListPrice MONEY);

INSERT lesson08.ProductStaging (ProductID, Name, ListPrice)
OUTPUT inserted.ProductID, inserted.Name, inserted.ListPrice
INTO   @Copied
SELECT ProductID, Name, ListPrice
FROM   lesson08.ProductStaging
WHERE  ListPrice > 50
  AND  ProductID NOT IN (SELECT ProductID FROM lesson08.ProductStaging WHERE ProductID > 9000);
-- Note: this would insert duplicates in practice; the exercise focuses on the OUTPUT syntax.

SELECT * FROM @Copied;


------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: Idempotent load
-- Reasoning: the tempting-but-wrong approach is a plain INSERT...SELECT of every staging
-- row, which duplicates on every re-run since ProductID isn't checked. NOT EXISTS against
-- the target on the natural key filters to genuinely new rows only, so re-running finds
-- nothing left to insert. OUTPUT INTO a table variable captures which keys landed without
-- a second SELECT. Target starts with 1, 2, 3; staging has 1, 2, 4, 5 — so run 1 inserts
-- 4 and 5, and run 2 (below) inserts nothing.
DECLARE @Inserted1 TABLE (ProductID INT);
INSERT lesson08.ProductPrice (ProductID, ListPrice, ModifiedDate)
OUTPUT inserted.ProductID INTO @Inserted1
SELECT s.ProductID, s.ListPrice, s.ModifiedDate
FROM lesson08.ProductPriceStage AS s
WHERE NOT EXISTS (
    SELECT 1 FROM lesson08.ProductPrice AS t WHERE t.ProductID = s.ProductID
);
SELECT * FROM @Inserted1;  -- run 1: ProductID 4 and 5

DECLARE @Inserted2 TABLE (ProductID INT);
INSERT lesson08.ProductPrice (ProductID, ListPrice, ModifiedDate)
OUTPUT inserted.ProductID INTO @Inserted2
SELECT s.ProductID, s.ListPrice, s.ModifiedDate
FROM lesson08.ProductPriceStage AS s
WHERE NOT EXISTS (
    SELECT 1 FROM lesson08.ProductPrice AS t WHERE t.ProductID = s.ProductID
);
SELECT * FROM @Inserted2;                              -- run 2: no rows
SELECT COUNT(*) AS SecondRunInsertCount FROM @Inserted2; -- must be 0

-- Challenge 2: Full sync with audit
-- Reasoning: MERGE is the right tool since it needs update+insert+delete in one atomic
-- statement sharing a single OUTPUT stream; three separate statements can't do that.
-- An extra staging row (ProductID 7) guarantees a genuine INSERT case; it's guarded by
-- NOT EXISTS so re-running the whole file doesn't hit a PK violation (same idempotent
-- habit Challenge 1 teaches). $action plus COALESCE(inserted.*, deleted.*) — only one
-- side is populated per action — go straight into the audit table via OUTPUT INTO.
INSERT lesson08.ProductPriceStage (ProductID, ListPrice, ModifiedDate)
SELECT 7, 70.00, SYSDATETIME()
WHERE NOT EXISTS (SELECT 1 FROM lesson08.ProductPriceStage WHERE ProductID = 7);

MERGE lesson08.ProductPrice AS t
USING lesson08.ProductPriceStage AS s
   ON s.ProductID = t.ProductID
WHEN MATCHED AND s.ListPrice <> t.ListPrice THEN
    UPDATE SET t.ListPrice = s.ListPrice, t.ModifiedDate = s.ModifiedDate
WHEN NOT MATCHED BY TARGET THEN
    INSERT (ProductID, ListPrice, ModifiedDate)
    VALUES (s.ProductID, s.ListPrice, s.ModifiedDate)
WHEN NOT MATCHED BY SOURCE THEN
    DELETE
OUTPUT $action, COALESCE(inserted.ProductID, deleted.ProductID), deleted.ListPrice, inserted.ListPrice
INTO lesson08.ProductPriceAudit (Action, ProductID, OldPrice, NewPrice);

-- Verify: one audit row per staged change (update/insert/delete all represented)
SELECT Action, ProductID, OldPrice, NewPrice FROM lesson08.ProductPriceAudit ORDER BY AuditID;
SELECT Action, COUNT(*) AS Cnt FROM lesson08.ProductPriceAudit GROUP BY Action;

-- Challenge 3: Dedupe in place
-- Reasoning: the tempting-but-wrong approach is GROUP BY + MIN(LogID) to find "the" survivor,
-- which can't express "keep the newest by ModifiedDate" without a second correlated query.
-- ROW_NUMBER() PARTITION BY the natural key, ORDER BY ModifiedDate DESC numbers the newest
-- row 1 per key; deleting through the CTE where rn > 1 is a windowed DELETE — you can't put
-- a window function directly in DELETE's WHERE (lesson 07 pitfall), so the CTE is required.
INSERT lesson08.ProductPriceLog (ProductID, ListPrice, ModifiedDate)
VALUES (101, 10.00, '2026-01-01'),
       (101, 10.50, '2026-02-01'),
       (101, 11.00, '2026-03-01'),
       (102, 20.00, '2026-01-15'),
       (102, 21.00, '2026-02-15');

;WITH Ranked AS (
    SELECT LogID, ProductID,
           ROW_NUMBER() OVER (PARTITION BY ProductID ORDER BY ModifiedDate DESC) AS rn
    FROM lesson08.ProductPriceLog
)
DELETE FROM Ranked WHERE rn > 1;

-- Verify: exactly one surviving row per key
SELECT ProductID, COUNT(*) AS RowsPerKey
FROM lesson08.ProductPriceLog
GROUP BY ProductID
ORDER BY ProductID;

-- Challenge 4: Batch delete
-- Reasoning: one giant DELETE holds its locks (risking escalation to table level) and
-- keeps its whole log record un-reusable for the entire statement, blocking concurrent
-- readers/writers and risking log growth. Deleting TOP (500) at a time in a loop lets
-- each batch's locks and log space free up before the next batch starts — the same
-- trade-off lessons 16 (locking/blocking) and 17 (waits) cover in depth; this is the
-- practitioner's preview.
INSERT lesson08.PurgeCandidate (Note)
SELECT TOP (2200) 'purge-' + CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS VARCHAR(10))
FROM sys.all_objects AS a CROSS JOIN sys.all_objects AS b;

DECLARE @RowsDeleted INT = 1, @BatchNum INT = 0;
WHILE @RowsDeleted > 0
BEGIN
    SET @BatchNum += 1;
    DELETE TOP (500) FROM lesson08.PurgeCandidate;
    SET @RowsDeleted = @@ROWCOUNT;
    PRINT CONCAT('Batch ', @BatchNum, ': deleted ', @RowsDeleted, ' rows');
END

SELECT COUNT(*) AS RemainingRows FROM lesson08.PurgeCandidate;  -- must be 0
