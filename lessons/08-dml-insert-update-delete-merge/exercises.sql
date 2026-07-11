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


------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: Idempotent load: insert into the lesson08 target only the staging rows
--              not already present (key match), capturing inserted keys via OUTPUT into
--              a table variable and selecting them. Run it twice — second run must
--              insert 0 rows.
-- Expected columns: (inserted keys result set)
-- Hint: INSERT ... SELECT ... WHERE NOT EXISTS.
-- Your query here:


-- Challenge 2: Full sync with audit: MERGE staging into target (update changed, insert
--              new, delete missing) writing $action plus old/new values via OUTPUT into
--              an audit table. Verify the audit row counts match the changes you staged.
-- Expected columns: (audit table contents)
-- Hint: OUTPUT $action, deleted.*, inserted.*.
-- Your query here:


-- Challenge 3: Dedupe in place: seed duplicate rows (same natural key, different
--              ModifiedDate) in a lesson08 table, then delete all but the newest per key
--              using a CTE over ROW_NUMBER — a windowed DELETE. Prove with a count per
--              key = 1.
-- Expected columns: (post-delete verification counts)
-- Hint: DELETE FROM cte WHERE rn > 1.
-- Your query here:


-- Challenge 4: Batch delete: delete a large set in chunks of 500 rows in a WHILE loop
--              (using TOP), reporting rows deleted per iteration, and explain in the
--              comment why chunking beats one giant DELETE (lock escalation, log growth
--              — lessons 16/17 preview).
-- Expected columns: (iteration report via PRINT/SELECT)
-- Hint: WHILE @@ROWCOUNT ... DELETE TOP (500).
-- Your query here:
