SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Reports', N'U') IS NULL OR OBJECT_ID(N'dbo.ReportAttachments', N'U') IS NULL
        THROW 50020, 'Migration failed: Reports or ReportAttachments does not exist.', 1;

    IF COL_LENGTH(N'dbo.Reports', N'ExpectedImageCount') IS NULL
        ALTER TABLE dbo.Reports ADD ExpectedImageCount INT NULL;

    IF COL_LENGTH(N'dbo.Reports', N'ExpectedVoiceNoteCount') IS NULL
        ALTER TABLE dbo.Reports ADD ExpectedVoiceNoteCount INT NULL;

    IF COL_LENGTH(N'dbo.ReportAttachments', N'ClientAttachmentId') IS NULL
        ALTER TABLE dbo.ReportAttachments ADD ClientAttachmentId NVARCHAR(100) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID(N'dbo.Reports') AND name = N'CK_Reports_ExpectedEvidenceCounts')
        EXEC (N'ALTER TABLE dbo.Reports WITH CHECK ADD CONSTRAINT CK_Reports_ExpectedEvidenceCounts CHECK ((ExpectedImageCount IS NULL OR ExpectedImageCount >= 0) AND (ExpectedVoiceNoteCount IS NULL OR ExpectedVoiceNoteCount >= 0));');

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ReportAttachments') AND name = N'UX_ReportAttachments_Report_ClientAttachmentId')
        EXEC (N'CREATE UNIQUE INDEX UX_ReportAttachments_Report_ClientAttachmentId ON dbo.ReportAttachments (ReportID, ClientAttachmentId) WHERE ClientAttachmentId IS NOT NULL;');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
