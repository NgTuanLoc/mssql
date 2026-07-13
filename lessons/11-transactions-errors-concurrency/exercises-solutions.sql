USE AdventureWorks2022;
GO

-- Exercise 1: Transfer $200 with manual rollback on insufficient funds.
-- Approach: check balance after update; if negative, rollback.
BEGIN TRANSACTION;

UPDATE lesson11.Account SET Balance = Balance - 200 WHERE AccountID = 1;

IF (SELECT Balance FROM lesson11.Account WHERE AccountID = 1) < 0
BEGIN
    ROLLBACK TRANSACTION;
    PRINT 'Insufficient funds.';
END
ELSE
BEGIN
    UPDATE lesson11.Account SET Balance = Balance + 200 WHERE AccountID = 2;
    COMMIT TRANSACTION;
    PRINT 'Transfer complete.';
END;

-- Exercise 2: Transfer with TRY/CATCH and table-variable log.
-- Approach: capture the committed transfer in a table variable using OUTPUT.
DECLARE @Log TABLE (TransferID INT IDENTITY(1,1), Amount MONEY, TransferredAt DATETIME2);

BEGIN TRY
    BEGIN TRANSACTION;
    UPDATE lesson11.Account SET Balance = Balance - 200 WHERE AccountID = 1;

    IF (SELECT Balance FROM lesson11.Account WHERE AccountID = 1) < 0
        THROW 50000, 'Insufficient funds.', 1;

    UPDATE lesson11.Account SET Balance = Balance + 200 WHERE AccountID = 2;
    COMMIT TRANSACTION;

    INSERT @Log (Amount, TransferredAt) VALUES (200, SYSDATETIME());
    PRINT 'Transfer logged.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT 'Error: ' + ERROR_MESSAGE();
END CATCH;

SELECT * FROM @Log;

-- Exercise 3: Deliberate divide-by-zero, caught and reported.
-- Approach: SELECT 1/0 triggers error 8134; CATCH reports without re-throwing.
BEGIN TRY
    SELECT 1 / 0 AS Oops;
END TRY
BEGIN CATCH
    PRINT 'Error number: ' + CAST(ERROR_NUMBER()   AS VARCHAR);
    PRINT 'Message:      ' + ERROR_MESSAGE();
    PRINT 'Line:         ' + CAST(ERROR_LINE()     AS VARCHAR);
END CATCH;

-- Exercise 4: usp_Transfer stored procedure.
-- Approach: THROW with a user-defined error number (50001–2147483647) and severity 16.
GO  -- CREATE/ALTER PROCEDURE must be the first statement in its batch.
CREATE OR ALTER PROCEDURE lesson11.usp_Transfer
    @FromID INT,
    @ToID   INT,
    @Amount MONEY
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE lesson11.Account SET Balance = Balance - @Amount WHERE AccountID = @FromID;

        IF (SELECT Balance FROM lesson11.Account WHERE AccountID = @FromID) < 0
            THROW 50001, 'Insufficient balance for the requested transfer.', 1;

        UPDATE lesson11.Account SET Balance = Balance + @Amount WHERE AccountID = @ToID;

        COMMIT TRANSACTION;
        RETURN 0;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;  -- re-raise to caller
    END CATCH;
END;
GO

EXEC lesson11.usp_Transfer @FromID = 1, @ToID = 2, @Amount = 5000;  -- should fail
GO
EXEC lesson11.usp_Transfer @FromID = 1, @ToID = 2, @Amount = 50;    -- should succeed
GO
SELECT * FROM lesson11.Account;

-- Exercise 5: Current session isolation level.
-- Approach: sys.dm_exec_sessions; @@SPID = current session ID.
SELECT
    session_id,
    login_name,
    CASE transaction_isolation_level
        WHEN 0 THEN 'Unspecified'
        WHEN 1 THEN 'Read Uncommitted'
        WHEN 2 THEN 'Read Committed'
        WHEN 3 THEN 'Repeatable Read'
        WHEN 4 THEN 'Serializable'
        WHEN 5 THEN 'Snapshot'
    END AS IsolationLevel
FROM sys.dm_exec_sessions
WHERE session_id = @@SPID;

GO
------------------------------------------------------------
-- CHALLENGES
-- Harder problems. They may combine this lesson's topic with
-- earlier lessons, and usually need 2+ concepts in one query.
------------------------------------------------------------

-- Challenge 1: Deadlock-retry wrapper procedure + demo call.
-- Reasoning: A deadlock (1205) is transient — the victim's locks are gone after
--   rollback, so re-running the same transaction usually succeeds; other errors
--   (constraint, conversion) are deterministic and would just fail again, so we
--   rethrow them at once. A WHILE loop with an @retry counter wraps the TRY/CATCH,
--   BREAK on success. Tempting-but-wrong: retrying EVERY error, turning a permanent
--   failure into a loop of guaranteed failures.
CREATE OR ALTER PROCEDURE lesson11.usp_TransferWithRetry
    @FromID INT, @ToID INT, @Amount DECIMAL(14,2)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @retry INT = 3;
    WHILE @retry > 0
    BEGIN
        BEGIN TRY
            BEGIN TRAN;
            UPDATE lesson11.Account SET Balance = Balance - @Amount WHERE AccountID = @FromID;
            UPDATE lesson11.Account SET Balance = Balance + @Amount WHERE AccountID = @ToID;
            COMMIT;
            BREAK;                       -- success: leave the retry loop
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0 ROLLBACK;
            IF ERROR_NUMBER() = 1205 AND @retry > 1
            BEGIN
                SET @retry -= 1;
                WAITFOR DELAY '00:00:00.5';   -- back off, then retry
            END
            ELSE THROW;                  -- not a deadlock (or out of tries): rethrow
        END CATCH
    END
END;
GO
-- Demo: reset the two balances for a deterministic result, then transfer 100.
UPDATE lesson11.Account SET Balance = 1000 WHERE AccountID = 1;
UPDATE lesson11.Account SET Balance = 500  WHERE AccountID = 2;
EXEC lesson11.usp_TransferWithRetry @FromID = 1, @ToID = 2, @Amount = 100;
SELECT AccountID, Balance FROM lesson11.Account WHERE AccountID IN (1, 2);  -- 900 / 600
GO

-- Challenge 2: Doomed-transaction handling (XACT_STATE() = -1, COMMIT -> 3930).
-- Reasoning: Under XACT_ABORT ON a run-time error dooms the transaction instead of
--   just terminating the statement — XACT_STATE() returns -1. A doomed tran can only
--   be rolled back, so COMMIT raises 3930; we catch that in a nested TRY/CATCH to
--   prove it, then ROLLBACK cleanly. Tempting-but-wrong: assuming the CATCH can
--   salvage and COMMIT the partial work — it cannot.
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRAN;
    DECLARE @x INT = CAST('not-a-number' AS INT);   -- conversion error dooms the tran
    COMMIT;
END TRY
BEGIN CATCH
    PRINT CONCAT('XACT_STATE() = ', XACT_STATE(), '   (-1 = doomed)');
    PRINT CONCAT('@@TRANCOUNT   = ', @@TRANCOUNT);
    BEGIN TRY
        COMMIT;                          -- illegal on a doomed transaction
    END TRY
    BEGIN CATCH
        PRINT CONCAT('COMMIT rejected: Msg ', ERROR_NUMBER(), ' - ', ERROR_MESSAGE());
    END CATCH;
    IF XACT_STATE() <> 0 ROLLBACK;
    PRINT CONCAT('After ROLLBACK, @@TRANCOUNT = ', @@TRANCOUNT);
END CATCH;
SET XACT_ABORT OFF;                      -- restore the session default for later challenges
GO

-- Challenge 3: Two-session RCSI observation (RUN THE TWO SCRIPTS IN SEPARATE SESSIONS).
-- Reasoning: AdventureWorks2022 already has RCSI on (sys.databases), so plain Read
--   Committed reads the last committed version instantly instead of blocking; to SEE
--   lock-based blocking we force it with the READCOMMITTEDLOCK hint. SNAPSHOT needs
--   ALLOW_SNAPSHOT_ISOLATION ON, so we enable it and revert afterward. Two sessions
--   are required — a single batch cannot block on itself.
--
-- Step 0 (once): confirm settings, then allow SNAPSHOT. Revert in Step 5.
--   SELECT is_read_committed_snapshot_on, snapshot_isolation_state_desc
--   FROM sys.databases WHERE name = 'AdventureWorks2022';   -- expect: 1, OFF
--   ALTER DATABASE AdventureWorks2022 SET ALLOW_SNAPSHOT_ISOLATION ON;
--
-- Step 1 -- Session A: open a transaction and hold it (do NOT commit yet).
--   BEGIN TRANSACTION;
--   UPDATE lesson11.Account SET Balance = 9999 WHERE AccountID = 1;
--   -- leave this window open; run Session B now, then come back and ROLLBACK.
--
-- Step 2 -- Session B, forced lock-based Read Committed: BLOCKS on A's write lock.
--   SET LOCK_TIMEOUT 4000;   -- so the block ends in an observable timeout, not a hang
--   SELECT AccountID, Balance FROM lesson11.Account WITH (READCOMMITTEDLOCK) WHERE AccountID = 1;
--   -- Observed: waits ~4s, then "Msg 1222 ... Lock request time out period exceeded."
--
-- Step 3 -- Session B, SNAPSHOT: reads the OLD committed value instantly, no blocking.
--   SET TRANSACTION ISOLATION LEVEL SNAPSHOT;
--   BEGIN TRANSACTION;
--   SELECT AccountID, Balance FROM lesson11.Account WHERE AccountID = 1;  -- returns 1000.00 at once
--   COMMIT;
--
-- Step 4 -- Session A: finish the transaction.
--   ROLLBACK TRANSACTION;   -- (or COMMIT) releases the lock
--
-- Step 5 (once): revert the database to how it was found.
--   ALTER DATABASE AdventureWorks2022 SET ALLOW_SNAPSHOT_ISOLATION OFF;

-- Challenge 4: Savepoints — commit 2 of 3 orders, roll back only the invalid one.
-- Reasoning: SAVE TRANSACTION marks a rollback point inside one transaction, so
--   ROLLBACK TRANSACTION <sp> undoes just the work after that savepoint without
--   killing the tran (a plain ROLLBACK would discard all 3 rows). Each insert gets
--   its own savepoint + TRY/CATCH; the invalid one rolls back to its savepoint, the
--   valid ones commit. Needs XACT_ABORT OFF, else the CHECK violation would doom the
--   tran and forbid the savepoint rollback.
SET XACT_ABORT OFF;
DELETE FROM lesson11.OrderStaging WHERE OrderID IN (101, 102, 103);  -- make re-runnable
BEGIN TRAN;
    SAVE TRAN sp1;
    BEGIN TRY INSERT lesson11.OrderStaging VALUES (101, N'Widget', 5); END TRY
    BEGIN CATCH ROLLBACK TRAN sp1; END CATCH;

    SAVE TRAN sp2;
    BEGIN TRY INSERT lesson11.OrderStaging VALUES (102, N'Gadget', 0); END TRY   -- Qty 0 fails
    BEGIN CATCH
        PRINT CONCAT('Order 102 rejected: ', ERROR_MESSAGE());
        ROLLBACK TRAN sp2;               -- undo only this insert
    END CATCH;

    SAVE TRAN sp3;
    BEGIN TRY INSERT lesson11.OrderStaging VALUES (103, N'Sprocket', 2); END TRY
    BEGIN CATCH ROLLBACK TRAN sp3; END CATCH;
COMMIT;
SELECT COUNT(*) AS RowsPersisted FROM lesson11.OrderStaging;               -- expect 2
SELECT OrderID, Product, Qty FROM lesson11.OrderStaging ORDER BY OrderID;  -- 101, 103
GO
