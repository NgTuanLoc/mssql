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
