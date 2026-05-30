USE AdventureWorks2022;
GO

-- =========================================================================
-- TRANSACTIONS
-- =========================================================================

-- Example 1: Basic BEGIN / COMMIT / ROLLBACK
BEGIN TRANSACTION;

UPDATE lesson11.Account SET Balance = Balance - 100 WHERE AccountID = 1; -- Alice pays
UPDATE lesson11.Account SET Balance = Balance + 100 WHERE AccountID = 2; -- Bob receives

-- Verify before committing
SELECT * FROM lesson11.Account WHERE AccountID IN (1, 2);

COMMIT TRANSACTION;
-- Both updates are now permanent.

-- Example 2: ROLLBACK on error
BEGIN TRANSACTION;

UPDATE lesson11.Account SET Balance = Balance - 2000 WHERE AccountID = 1;
-- This would violate the CHECK constraint (Balance < 0)

IF @@ROWCOUNT = 0 OR (SELECT Balance FROM lesson11.Account WHERE AccountID = 1) < 0
BEGIN
    ROLLBACK TRANSACTION;
    PRINT 'Transfer cancelled — insufficient funds.';
END
ELSE
    COMMIT TRANSACTION;

-- =========================================================================
-- TRY / CATCH
-- =========================================================================

-- Example 3: TRY/CATCH with THROW
BEGIN TRY
    BEGIN TRANSACTION;

    UPDATE lesson11.Account SET Balance = Balance - 300 WHERE AccountID = 1;
    UPDATE lesson11.Account SET Balance = Balance + 300 WHERE AccountID = 3;

    COMMIT TRANSACTION;
    PRINT 'Transfer complete.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT 'Error caught: ' + ERROR_MESSAGE();
    THROW;   -- re-raise to caller
END CATCH;

-- Example 4: TRY/CATCH — trigger a CHECK constraint violation deliberately
BEGIN TRY
    UPDATE lesson11.Account SET Balance = -1 WHERE AccountID = 1;
END TRY
BEGIN CATCH
    PRINT 'Severity:  ' + CAST(ERROR_SEVERITY() AS VARCHAR);
    PRINT 'State:     ' + CAST(ERROR_STATE()    AS VARCHAR);
    PRINT 'Message:   ' + ERROR_MESSAGE();
    PRINT 'Line:      ' + CAST(ERROR_LINE()     AS VARCHAR);
END CATCH;

-- =========================================================================
-- ISOLATION LEVELS  (run each block in a separate SSMS window for demos)
-- =========================================================================

-- Example 5: READ COMMITTED (default) — cannot read uncommitted data
-- Session 1:
BEGIN TRANSACTION;
UPDATE lesson11.Account SET Balance = 9999 WHERE AccountID = 1;
-- Do NOT commit yet. Switch to Session 2:
-- Session 2: SELECT * FROM lesson11.Account WHERE AccountID = 1;
-- Session 2 blocks until Session 1 commits or rolls back.
-- Session 1:
ROLLBACK;

-- Example 6: READ COMMITTED SNAPSHOT (RCSI) — non-blocking reads
-- Enable RCSI on the database (run once; requires no active connections):
-- ALTER DATABASE AdventureWorks2022 SET READ_COMMITTED_SNAPSHOT ON;
-- With RCSI, Session 2's SELECT above would return the committed value (1000)
-- instead of blocking.

-- Example 7: Check current isolation level
SELECT
    session_id,
    transaction_isolation_level,
    CASE transaction_isolation_level
        WHEN 0 THEN 'Unspecified'
        WHEN 1 THEN 'Read Uncommitted'
        WHEN 2 THEN 'Read Committed'
        WHEN 3 THEN 'Repeatable Read'
        WHEN 4 THEN 'Serializable'
        WHEN 5 THEN 'Snapshot'
    END AS IsolationLevelName
FROM sys.dm_exec_sessions
WHERE session_id = @@SPID;
