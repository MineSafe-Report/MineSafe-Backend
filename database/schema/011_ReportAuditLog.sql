CREATE TABLE ReportAuditLog (
    AuditLogID INT IDENTITY(1,1) NOT NULL,
    ReportID INT NOT NULL,
    ActorAccountID INT NOT NULL,
    Action NVARCHAR(50) NOT NULL,
    OldValueJSON NVARCHAR(MAX) NULL,
    NewValueJSON NVARCHAR(MAX) NULL,
    ActionAt DATETIME2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_ReportAuditLog PRIMARY KEY (AuditLogID),
    CONSTRAINT FK_ReportAuditLog_Reports FOREIGN KEY (ReportID) REFERENCES Reports(ReportID),
    CONSTRAINT FK_ReportAuditLog_Accounts FOREIGN KEY (ActorAccountID) REFERENCES Accounts(AccountID)
);
