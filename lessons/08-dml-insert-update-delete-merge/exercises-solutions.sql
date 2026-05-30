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
