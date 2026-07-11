USE AdventureWorks2022;
GO

IF SCHEMA_ID('lesson03') IS NOT NULL
BEGIN
    -- Drop all tables in the schema before dropping the schema
    DECLARE @sql NVARCHAR(MAX) = N'';
    SELECT @sql += 'DROP TABLE lesson03.' + QUOTENAME(name) + ';' + CHAR(10)
    FROM sys.tables
    WHERE schema_id = SCHEMA_ID('lesson03');
    EXEC sp_executesql @sql;

    DROP SCHEMA lesson03;
END
GO
CREATE SCHEMA lesson03;
GO

-- Demo table for implicit conversion and type behaviour examples
CREATE TABLE lesson03.TypeDemo (
    ID              INT             IDENTITY(1,1) PRIMARY KEY,
    VarcharCode     VARCHAR(20)     NOT NULL,
    NVarcharName    NVARCHAR(100)   NOT NULL,
    PriceDecimal    DECIMAL(10,4)   NOT NULL,
    PriceMoney      MONEY           NOT NULL,
    BirthDate       DATE            NOT NULL,
    CreatedAt       DATETIME2(7)    NOT NULL DEFAULT SYSDATETIME(),
    IsActive        BIT             NOT NULL DEFAULT 1
);

INSERT lesson03.TypeDemo (VarcharCode, NVarcharName, PriceDecimal, PriceMoney, BirthDate)
VALUES
    ('A001', N'Alice',   1234.5678, 1234.5678, '1990-06-15'),
    ('B002', N'Bob',     999.9999,  999.9999,  '1985-11-30'),
    ('C003', N'Café',    0.0100,    0.0100,    '2000-01-01'),
    ('D004', N'正確',    50000.00,  50000.00,  '1975-03-22');

-- Messy date strings in mixed formats, for Challenge 4 (TRY_CONVERT style-chasing).
CREATE TABLE lesson03.MixedDates (
    ID       INT           IDENTITY(1,1) PRIMARY KEY,
    RawValue VARCHAR(20)   NOT NULL
);

INSERT lesson03.MixedDates (RawValue)
VALUES
    ('2014-03-01'),   -- ISO (style 120)
    ('03/15/2014'),   -- US slash (style 101)
    ('20140401'),     -- compact (style 112)
    ('not a date');   -- unparseable by any style

PRINT 'Lesson 03 setup complete.';
