USE AdventureWorks2022;
GO

-- Example 1: CREATE TABLE with common constraints
DROP TABLE IF EXISTS lesson09.Orders;
DROP TABLE IF EXISTS lesson09.Customers;
GO

CREATE TABLE lesson09.Customers (
    CustomerID   INT           IDENTITY(1,1) PRIMARY KEY,
    Email        NVARCHAR(200) NOT NULL,
    FullName     NVARCHAR(200) NOT NULL,
    CreatedAt    DATETIME2(7)  NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT UQ_Customers_Email UNIQUE (Email)
);

CREATE TABLE lesson09.Orders (
    OrderID      INT           IDENTITY(1,1) PRIMARY KEY,
    CustomerID   INT           NOT NULL,
    OrderDate    DATETIME2(7)  NOT NULL DEFAULT SYSDATETIME(),
    TotalAmount  DECIMAL(14,2) NOT NULL,
    Status       TINYINT       NOT NULL DEFAULT 1,
    CONSTRAINT FK_Orders_Customers FOREIGN KEY (CustomerID)
        REFERENCES lesson09.Customers (CustomerID),
    CONSTRAINT CK_Orders_TotalAmount CHECK (TotalAmount >= 0),
    CONSTRAINT CK_Orders_Status      CHECK (Status BETWEEN 1 AND 5)
);

-- Example 2: INSERT data
INSERT lesson09.Customers (Email, FullName)
VALUES (N'alice@example.com', N'Alice Smith'),
       (N'bob@example.com',   N'Bob Jones');

INSERT lesson09.Orders (CustomerID, TotalAmount)
VALUES (1, 250.00), (1, 89.50), (2, 1200.00);

-- Example 3: IDENTITY — see current seed and increment
DBCC CHECKIDENT('lesson09.Customers', NORESEED);
-- Reseed example (dangerous in production — demo only)
-- DBCC CHECKIDENT('lesson09.Customers', RESEED, 1000);

-- Example 4: Computed column
DROP TABLE IF EXISTS lesson09.Invoice;
GO
CREATE TABLE lesson09.Invoice (
    InvoiceID    INT           IDENTITY(1,1) PRIMARY KEY,
    Quantity     INT           NOT NULL,
    UnitPrice    DECIMAL(10,2) NOT NULL,
    LineTotal    AS (Quantity * UnitPrice) PERSISTED   -- stored on disk
);

INSERT lesson09.Invoice (Quantity, UnitPrice) VALUES (3, 25.00), (10, 4.99);
SELECT * FROM lesson09.Invoice;   -- LineTotal computed automatically

-- Example 5: ALTER TABLE — add a column and a constraint
ALTER TABLE lesson09.Customers
    ADD PhoneNumber NVARCHAR(20) NULL;

ALTER TABLE lesson09.Orders
    ADD CONSTRAINT DF_Orders_TotalAmount DEFAULT (0) FOR TotalAmount;

-- Example 6: Schemas — create and assign a table to a non-default schema
-- (lesson09 already exists; this shows the syntax)
SELECT SCHEMA_NAME(schema_id) AS SchemaName, name AS TableName
FROM sys.tables
WHERE schema_id = SCHEMA_ID('lesson09');

-- Example 7: Sequence object (alternative to IDENTITY for shared sequences)
DROP SEQUENCE IF EXISTS lesson09.InvoiceSeq;
CREATE SEQUENCE lesson09.InvoiceSeq
    AS INT
    START WITH 1000
    INCREMENT BY 1
    NO CYCLE;

SELECT NEXT VALUE FOR lesson09.InvoiceSeq AS NextID;
SELECT NEXT VALUE FOR lesson09.InvoiceSeq AS NextID;
