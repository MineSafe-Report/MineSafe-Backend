-- Adds the server-side fields required for idempotent mobile report synchronization.
-- Existing ClientReportId values are generated because historical reports predate
-- client-generated identifiers. Existing OccurredAt values use CreatedAt as the
-- closest guaranteed timestamp; this is an approximation, not a known incident time.

SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Reports', N'U') IS NULL
        THROW 50001, 'Migration failed: dbo.Reports does not exist.', 1;

    IF COL_LENGTH(N'dbo.Reports', N'ClientReportId') IS NULL
    BEGIN
        ALTER TABLE dbo.Reports
            ADD ClientReportId UNIQUEIDENTIFIER NULL;
    END
    ELSE IF EXISTS (
        SELECT 1
        FROM sys.columns AS c
        INNER JOIN sys.types AS t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID(N'dbo.Reports')
          AND c.name = N'ClientReportId'
          AND t.name <> N'uniqueidentifier'
    )
        THROW 50002, 'Migration failed: dbo.Reports.ClientReportId has an unexpected data type.', 1;

    IF COL_LENGTH(N'dbo.Reports', N'OccurredAt') IS NULL
    BEGIN
        ALTER TABLE dbo.Reports
            ADD OccurredAt DATETIME2(0) NULL;
    END
    ELSE IF EXISTS (
        SELECT 1
        FROM sys.columns AS c
        INNER JOIN sys.types AS t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID(N'dbo.Reports')
          AND c.name = N'OccurredAt'
          AND (t.name <> N'datetime2' OR c.scale <> 0)
    )
        THROW 50003, 'Migration failed: dbo.Reports.OccurredAt must be DATETIME2(0).', 1;

    UPDATE dbo.Reports
    SET ClientReportId = NEWID()
    WHERE ClientReportId IS NULL;

    UPDATE dbo.Reports
    SET OccurredAt = CreatedAt
    WHERE OccurredAt IS NULL;

    IF EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.Reports')
          AND name = N'ClientReportId'
          AND is_nullable = 1
    )
    BEGIN
        ALTER TABLE dbo.Reports
            ALTER COLUMN ClientReportId UNIQUEIDENTIFIER NOT NULL;
    END;

    IF EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.Reports')
          AND name = N'OccurredAt'
          AND is_nullable = 1
    )
    BEGIN
        ALTER TABLE dbo.Reports
            ALTER COLUMN OccurredAt DATETIME2(0) NOT NULL;
    END;

    IF EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Reports')
          AND name = N'UQ_Reports_ClientReportId'
    )
    AND NOT EXISTS (
        SELECT 1
        FROM sys.indexes AS i
        INNER JOIN sys.index_columns AS ic
            ON ic.object_id = i.object_id
           AND ic.index_id = i.index_id
        INNER JOIN sys.columns AS c
            ON c.object_id = ic.object_id
           AND c.column_id = ic.column_id
        WHERE i.object_id = OBJECT_ID(N'dbo.Reports')
          AND i.name = N'UQ_Reports_ClientReportId'
          AND i.is_unique = 1
          AND i.has_filter = 0
          AND ic.is_included_column = 0
        GROUP BY i.index_id
        HAVING COUNT(*) = 1
           AND MAX(CASE WHEN c.name = N'ClientReportId' THEN 1 ELSE 0 END) = 1
    )
        THROW 50004, 'Migration failed: UQ_Reports_ClientReportId exists but does not uniquely cover only ClientReportId.', 1;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes AS i
        INNER JOIN sys.index_columns AS ic
            ON ic.object_id = i.object_id
           AND ic.index_id = i.index_id
        INNER JOIN sys.columns AS c
            ON c.object_id = ic.object_id
           AND c.column_id = ic.column_id
        WHERE i.object_id = OBJECT_ID(N'dbo.Reports')
          AND i.is_unique = 1
          AND i.has_filter = 0
          AND ic.is_included_column = 0
        GROUP BY i.index_id
        HAVING COUNT(*) = 1
           AND MAX(CASE WHEN c.name = N'ClientReportId' THEN 1 ELSE 0 END) = 1
    )
    BEGIN
        ALTER TABLE dbo.Reports
            ADD CONSTRAINT UQ_Reports_ClientReportId UNIQUE (ClientReportId);
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
