USE AdventureWorks2022;
GO
-- Re-run setup.sql to reset Account balances before retrying exercises.

-- Exercise 1: Write a transaction that transfers $200 from AccountID 1 to AccountID 2.
--             If the transfer would leave AccountID 1 with a negative balance, ROLLBACK
--             and print 'Insufficient funds'. Otherwise COMMIT.
-- Your query here:


-- Exercise 2: Wrap Exercise 1 in TRY/CATCH. After a successful transfer, also insert
--             a log row into a table variable (columns: TransferID INT, Amount MONEY,
--             TransferredAt DATETIME2). SELECT from the table variable at the end.
-- Your query here:


-- Exercise 3: Deliberately cause a divide-by-zero error inside a TRY block.
--             In the CATCH block, print the error number, message, and line number.
--             Do NOT re-throw — just report and continue.
-- Your query here:


-- Exercise 4: Write a stored procedure lesson11.usp_Transfer that:
--             - Accepts @FromID INT, @ToID INT, @Amount MONEY
--             - Uses TRY/CATCH and a transaction
--             - THROWs a custom error (number 50001, severity 16, state 1) if
--               the source account doesn't have enough balance
--             - Returns 0 on success
-- Test: EXEC lesson11.usp_Transfer @FromID=1, @ToID=2, @Amount=5000 (should fail)
--       EXEC lesson11.usp_Transfer @FromID=1, @ToID=2, @Amount=50   (should succeed)
-- Your query here:


-- Exercise 5: Using sys.dm_exec_sessions, write a query that shows your current
--             session ID, isolation level name, and login name.
-- Your query here:
