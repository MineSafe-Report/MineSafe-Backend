SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.AccountLocations', N'U') IS NULL OR OBJECT_ID(N'dbo.ReportReviews', N'U') IS NULL OR OBJECT_ID(N'dbo.ReportAttachments', N'U') IS NULL
        THROW 50010, 'Migration failed: one or more required tables do not exist.', 1;

    IF EXISTS (
        SELECT AccountID FROM dbo.AccountLocations WHERE IsDefault = 1 AND IsActive = 1 GROUP BY AccountID HAVING COUNT(*) > 1 )
        THROW 50011, 'Migration failed: an account has multiple active default locations.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.AccountLocations') AND name = N'UX_AccountLocations_OneActiveDefaultPerAccount' )
        CREATE UNIQUE INDEX UX_AccountLocations_OneActiveDefaultPerAccount ON dbo.AccountLocations (AccountID) WHERE IsDefault = 1 AND IsActive = 1;

    IF EXISTS (SELECT 1 FROM dbo.ReportReviews WHERE Decision NOT IN (N'APPROVED', N'DENIED'))
        THROW 50012, 'Migration failed: ReportReviews contains an unsupported Decision.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.ReportReviews') AND name = N'CK_ReportReviews_Decision' )
        ALTER TABLE dbo.ReportReviews WITH CHECK ADD CONSTRAINT CK_ReportReviews_Decision CHECK (Decision IN (N'APPROVED', N'DENIED'));

    IF EXISTS (
        SELECT 1 FROM dbo.ReportAttachments WHERE Sha256Hash IS NOT NULL AND (DATALENGTH(Sha256Hash) <> 64 OR Sha256Hash LIKE '%[^0-9A-Fa-f]%') )
        THROW 50013, 'Migration failed: ReportAttachments contains an invalid SHA-256 hash.', 1;

    IF NOT EXISTS (
        SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.ReportAttachments') AND name = N'CK_ReportAttachments_Sha256Hash' )
        ALTER TABLE dbo.ReportAttachments WITH CHECK ADD CONSTRAINT CK_ReportAttachments_Sha256Hash CHECK (Sha256Hash IS NULL OR
                   (DATALENGTH(Sha256Hash) = 64 AND Sha256Hash NOT LIKE '%[^0-9A-Fa-f]%'));

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
