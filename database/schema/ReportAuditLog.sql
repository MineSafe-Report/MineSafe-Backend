CREATE TABLE ReportAuditLog (
    AuditLogID INT IDENTITY(1,1) NOT NULL,
    ReportID INT NOT NULL,
    ActorAccountID INT NOT NULL,
    Action NVARCHAR(50) NOT NULL,
    OldValueJSON NVARCHAR(MAX) NULL,
    NewValueJSON NVARCHAR(MAX) NULL,
    ActionAt DATETIME2(0) NOT NULL CONSTRAINT DF_ReportAuditLog_ActionAt DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_ReportAuditLog PRIMARY KEY (AuditLogID),
    CONSTRAINT FK_ReportAuditLog_Reports FOREIGN KEY (ReportID) REFERENCES Reports(ReportID),
    CONSTRAINT FK_ReportAuditLog_Actor_Accounts FOREIGN KEY (ActorAccountID) REFERENCES Accounts(AccountID), CONSTRAINT CK_ReportAuditLog_OldValueJSON CHECK (OldValueJSON IS NULL OR ISJSON(OldValueJSON) = 1), CONSTRAINT CK_ReportAuditLog_NewValueJSON CHECK (NewValueJSON IS NULL OR ISJSON(NewValueJSON) = 1)
);

CREATE INDEX IX_ReportAuditLog_ReportID_ActionAt ON ReportAuditLog (ReportID, ActionAt);

CREATE INDEX IX_ReportAuditLog_ActorAccountID ON ReportAuditLog (ActorAccountID);

CREATE INDEX IX_ReportAuditLog_ActionAt ON ReportAuditLog (ActionAt);
