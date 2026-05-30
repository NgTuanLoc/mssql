USE AdventureWorks2022;
GO
-- Re-run setup.sql first if you need a clean slate: .\scripts\reset-db.ps1 or re-run setup.sql

-- Exercise 1: Insert 3 new rows into lesson08.ProductStaging with ProductIDs 8001, 8002, 8003,
--             names 'Test A', 'Test B', 'Test C', and ListPrices 10.00, 20.00, 30.00.
-- Your query here:


-- Exercise 2: Update the ListPrice of ALL rows in lesson08.ProductStaging
--             where ProductID >= 8001 to ListPrice * 2 (double them).
--             Use OUTPUT to return the ProductID, old price, and new price.
-- Expected columns in output: ProductID, OldPrice, NewPrice
-- Your query here:


-- Exercise 3: Delete all rows from lesson08.ProductStaging where ListPrice = 0.
--             Use OUTPUT to capture the deleted ProductIDs and Names.
-- Your query here:


-- Exercise 4: Write a MERGE statement that uses lesson08.InventorySource as the source
--             and lesson08.InventoryTarget as the target.
--             - WHEN MATCHED AND stock differs: update StockQty and LastSyncAt
--             - WHEN NOT MATCHED BY TARGET: insert the new row
--             - WHEN NOT MATCHED BY SOURCE: do nothing (no DELETE this time)
-- Your query here:


-- Exercise 5: Using INSERT...SELECT and OUTPUT, copy all lesson08.ProductStaging rows
--             with ListPrice > 50 into a table variable, then SELECT from it.
-- Expected columns (in table variable and final SELECT): ProductID, Name, ListPrice
-- Your query here:
