CREATE TABLE Notifications (
    NotificationID INT IDENTITY(1,1) NOT NULL,
    RecipientAccountID INT NOT NULL,
    ReportID INT NOT NULL,
    NotificationType NVARCHAR(50) NOT NULL,
    Message NVARCHAR(500) NOT NULL,
    CreatedAt DATETIME2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    ReadAt DATETIME2(0) NULL,

    CONSTRAINT PK_Notifications PRIMARY KEY (NotificationID),
    CONSTRAINT FK_Notifications_Accounts FOREIGN KEY (RecipientAccountID) REFERENCES Accounts(AccountID),
    CONSTRAINT FK_Notifications_Reports FOREIGN KEY (ReportID) REFERENCES Reports(ReportID)
);
