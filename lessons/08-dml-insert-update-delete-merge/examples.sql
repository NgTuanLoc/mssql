USE AdventureWorks2022;
GO

-- Example 1: INSERT with explicit column list (safe against schema changes)
INSERT lesson08.ProductStaging (ProductID, Name, ListPrice)
VALUES (9999, N'Demo Product', 99.99);

-- Example 2: INSERT ... SELECT — bulk load from another table
INSERT lesson08.ProductStaging (ProductID, Name, ListPrice)
SELECT ProductID, Name, ListPrice
FROM Production.Product
WHERE ProductSubcategoryID = 1
  AND ProductID NOT IN (SELECT ProductID FROM lesson08.ProductStaging);

-- Example 3: UPDATE with FROM (joining another table)
UPDATE ps
SET    ps.ListPrice = p.ListPrice,   -- sync to source
       ps.Name      = p.Name
FROM   lesson08.ProductStaging AS ps
JOIN   Production.Product      AS p ON p.ProductID = ps.ProductID;

-- Example 4: UPDATE with OUTPUT — capture what changed
DECLARE @Changed TABLE (
    ProductID    INT,
    OldPrice     MONEY,
    NewPrice     MONEY
);

UPDATE lesson08.ProductStaging
SET    ListPrice = ListPrice * 1.05   -- 5% price increase
OUTPUT inserted.ProductID,
       deleted.ListPrice  AS OldPrice,
       inserted.ListPrice AS NewPrice
INTO   @Changed;

SELECT * FROM @Changed;

-- Example 5: DELETE with OUTPUT — capture deleted rows
DELETE lesson08.ProductStaging
OUTPUT deleted.ProductID, deleted.Name
WHERE  IsActive = 0;

-- Example 6: MERGE — upsert InventoryTarget from InventorySource
MERGE lesson08.InventoryTarget AS t
USING lesson08.InventorySource AS s
   ON s.ProductID = t.ProductID
WHEN MATCHED AND s.StockQty <> t.StockQty THEN
    UPDATE SET t.StockQty = s.StockQty, t.LastSyncAt = SYSDATETIME()
WHEN NOT MATCHED BY TARGET THEN
    INSERT (ProductID, StockQty, LastSyncAt)
    VALUES (s.ProductID, s.StockQty, SYSDATETIME())
WHEN NOT MATCHED BY SOURCE THEN
    DELETE
OUTPUT $action AS MergeAction, inserted.ProductID, deleted.ProductID;
