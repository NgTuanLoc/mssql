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


------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: Deadlock-retry wrapper. Write a stored procedure that performs a
--              two-table update (a transfer between two lesson11.Account rows)
--              inside a retry loop: on error 1205 (deadlock victim) it waits
--              WAITFOR DELAY '00:00:00.5' and retries, up to 3 attempts total;
--              any other error is rethrown immediately. Include the full
--              TRY/CATCH/retry skeleton and a comment on why only 1205 is retryable.
--              Then call it once to prove it commits on the first (non-deadlocked) try.
-- Expected: proc DDL + a demo EXEC that transfers 100 and shows the two balances.
-- Hint: WHILE loop with an @retry counter; CATCH checks ERROR_NUMBER() = 1205.
-- Your query here:


-- Challenge 2: Doomed-transaction handling. With SET XACT_ABORT ON, open a
--              transaction and hit a conversion error inside it. In the CATCH,
--              show that XACT_STATE() = -1 (the transaction is doomed), then PROVE
--              you can only ROLLBACK, not COMMIT: attempt a COMMIT and capture the
--              resulting error 3930, then roll back cleanly.
-- Expected: a demo sequence printing XACT_STATE(), the 3930 message, and final @@TRANCOUNT = 0.
-- Your query here:


-- Challenge 3: Two-session RCSI observation. Session A updates a lesson11.Account
--              row inside an OPEN transaction (no commit yet). Session B then reads
--              that row: once forcing lock-based Read Committed (it BLOCKS), and once
--              under SNAPSHOT isolation (it sees the OLD value instantly). Provide
--              both session scripts and the expected observation at each step.
-- Expected: two runnable scripts (Session A / Session B) + narrated expectations.
-- Hint: Check RCSI/snapshot settings on AdventureWorks2022 first (sys.databases).
--       SNAPSHOT needs ALLOW_SNAPSHOT_ISOLATION ON — enable it, then revert after.
-- Your query here:


-- Challenge 4: Savepoints. In ONE transaction, insert a batch of 3 orders into
--              lesson11.OrderStaging where the 2nd order fails validation (Qty <= 0
--              violates the CHECK constraint). Use SAVE TRANSACTION before each
--              insert, roll back ONLY the failed one, commit the rest, and prove
--              that 2 of the 3 rows persisted.
-- Expected: final row count = 2, plus the two surviving rows.
-- Hint: SAVE TRANSACTION sp; ... ROLLBACK TRANSACTION sp; inside a per-row TRY/CATCH.
-- Your query here:
