CREATE TABLE ReportAttachments (
    AttachmentID INT IDENTITY(1,1) NOT NULL,
    ReportID INT NOT NULL,
    AttachmentType NVARCHAR(50) NOT NULL,
    OriginalFileName NVARCHAR(255) NOT NULL,
    StorageKey NVARCHAR(255) NOT NULL,
    MimeType NVARCHAR(100) NOT NULL,
    SizeBytes BIGINT NOT NULL,
    Sha256Hash CHAR(64) NULL,
    UploadedByAccount INT NOT NULL,
    UploadedAt DATETIME2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    IsDeleted BIT NOT NULL DEFAULT (0),

    CONSTRAINT PK_ReportAttachments PRIMARY KEY (AttachmentID),
    CONSTRAINT FK_ReportAttachments_Reports FOREIGN KEY (ReportID) REFERENCES Reports(ReportID),
    CONSTRAINT FK_ReportAttachments_Accounts FOREIGN KEY (UploadedByAccount) REFERENCES Accounts(AccountID)
);
