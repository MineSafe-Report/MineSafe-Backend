CREATE TABLE Notifications (
    NotificationID INT IDENTITY(1,1) NOT NULL,
    RecipientAccountID INT NOT NULL,
    ReportID INT NOT NULL,
    NotificationType NVARCHAR(50) NOT NULL,
    Message NVARCHAR(500) NOT NULL,
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Notifications_CreatedAt DEFAULT (SYSUTCDATETIME()),
    ReadAt DATETIME2(0) NULL,

    CONSTRAINT PK_Notifications PRIMARY KEY (NotificationID),
    CONSTRAINT FK_Notifications_Reports FOREIGN KEY (ReportID) REFERENCES Reports(ReportID),
    CONSTRAINT FK_Notifications_Recipient_Accounts FOREIGN KEY (RecipientAccountID) REFERENCES Accounts(AccountID)
);

CREATE INDEX IX_Notifications_RecipientAccountID_ReadAt_CreatedAt ON Notifications (RecipientAccountID, ReadAt, CreatedAt);

CREATE INDEX IX_Notifications_ReportID ON Notifications (ReportID);

CREATE INDEX IX_Notifications_CreatedAt ON Notifications (CreatedAt);
