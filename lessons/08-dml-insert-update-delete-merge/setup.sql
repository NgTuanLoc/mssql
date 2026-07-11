USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson08') IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP TABLE lesson08.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables WHERE schema_id = SCHEMA_ID('lesson08');
    EXEC sp_executesql @sql;
    DROP SCHEMA lesson08;
END
GO
CREATE SCHEMA lesson08;
GO

-- Staging table for INSERT/UPDATE/DELETE exercises (re-seeded on each setup run)
CREATE TABLE lesson08.ProductStaging (
    StagingID   INT          IDENTITY(1,1) PRIMARY KEY,
    ProductID   INT          NOT NULL,
    Name        NVARCHAR(50) NOT NULL,
    ListPrice   MONEY        NOT NULL,
    IsActive    BIT          NOT NULL DEFAULT 1,
    LoadedAt    DATETIME2    NOT NULL DEFAULT SYSDATETIME()
);

-- Target table for MERGE demo
CREATE TABLE lesson08.InventoryTarget (
    ProductID  INT          NOT NULL PRIMARY KEY,
    StockQty   INT          NOT NULL DEFAULT 0,
    LastSyncAt DATETIME2    NOT NULL DEFAULT SYSDATETIME()
);

-- Source data — simulates an incoming feed
CREATE TABLE lesson08.InventorySource (
    ProductID  INT NOT NULL PRIMARY KEY,
    StockQty   INT NOT NULL
);

INSERT lesson08.ProductStaging (ProductID, Name, ListPrice)
SELECT TOP 10 ProductID, Name, ListPrice
FROM Production.Product
WHERE ListPrice > 0
ORDER BY ProductID;

INSERT lesson08.InventoryTarget (ProductID, StockQty)
VALUES (1, 100), (2, 200), (3, 300);

INSERT lesson08.InventorySource (ProductID, StockQty)
VALUES (1, 95),    -- update
       (3, 310),   -- update
       (4, 50),    -- insert (new)
       (5, 0);     -- insert (new, zero stock)

------------------------------------------------------------
-- Added for the Challenges: a price target/staging pair with
-- an audit table, plus two scratch tables the dedupe and
-- batch-delete challenges seed for themselves.
------------------------------------------------------------

-- Challenge target: current prices "live" in the system
CREATE TABLE lesson08.ProductPrice (
    ProductID    INT       NOT NULL PRIMARY KEY,
    ListPrice    MONEY     NOT NULL,
    ModifiedDate DATETIME2 NOT NULL
);

-- Challenge staging: the incoming feed to reconcile against the target
CREATE TABLE lesson08.ProductPriceStage (
    ProductID    INT       NOT NULL PRIMARY KEY,
    ListPrice    MONEY     NOT NULL,
    ModifiedDate DATETIME2 NOT NULL
);

-- Audit trail written by the Challenge 2 MERGE via OUTPUT
CREATE TABLE lesson08.ProductPriceAudit (
    AuditID    INT IDENTITY(1,1) PRIMARY KEY,
    Action     NVARCHAR(10)  NOT NULL,
    ProductID  INT           NOT NULL,
    OldPrice   MONEY         NULL,
    NewPrice   MONEY         NULL,
    AuditAt    DATETIME2     NOT NULL DEFAULT SYSDATETIME()
);

-- ProductID 1 and 2 already exist in the target; 3 exists in the target but
-- will be absent from staging (the "missing" case); 4 and 5 exist only in
-- staging (the "new" case); 2's price differs between the two (the
-- "changed" case). This one seed drives both Challenge 1 (insert-missing)
-- and Challenge 2 (full sync) with genuine positive cases for every branch.
INSERT lesson08.ProductPrice (ProductID, ListPrice, ModifiedDate)
VALUES (1, 100.00, '2026-01-01'),
       (2, 150.00, '2026-01-01'),
       (3, 200.00, '2026-01-01');

INSERT lesson08.ProductPriceStage (ProductID, ListPrice, ModifiedDate)
VALUES (1, 100.00, '2026-02-01'),  -- unchanged price
       (2, 175.00, '2026-02-01'),  -- price changed 150 -> 175
       (4,  40.00, '2026-02-01'),  -- new key, not yet in target
       (5,  50.00, '2026-02-01');  -- new key, not yet in target

-- Dedupe scratch table for Challenge 3 (empty here — the challenge seeds
-- its own duplicate rows so the challenge is self-contained and re-runnable)
CREATE TABLE lesson08.ProductPriceLog (
    LogID        INT IDENTITY(1,1) PRIMARY KEY,
    ProductID    INT       NOT NULL,
    ListPrice    MONEY     NOT NULL,
    ModifiedDate DATETIME2 NOT NULL
);

-- Batch-delete scratch table for Challenge 4 (empty here — the challenge
-- seeds its own bulk rows so the challenge is self-contained and re-runnable)
CREATE TABLE lesson08.PurgeCandidate (
    PurgeID INT IDENTITY(1,1) PRIMARY KEY,
    Note    VARCHAR(50) NOT NULL
);

PRINT 'Lesson 08 setup complete.';
