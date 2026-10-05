CREATE TABLE ReportReviews (
    ReviewID INT IDENTITY(1,1) NOT NULL,
    ReportID INT NOT NULL,
    ReviewerAccountID INT NOT NULL,
    Decision NVARCHAR(50) NOT NULL,
    ReviewComment NVARCHAR(1000) NULL,
    ReviewedAt DATETIME2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT PK_ReportReviews PRIMARY KEY (ReviewID),
    CONSTRAINT FK_ReportReviews_Reports FOREIGN KEY (ReportID) REFERENCES Reports(ReportID),
    CONSTRAINT FK_ReportReviews_Accounts FOREIGN KEY (ReviewerAccountID) REFERENCES Accounts(AccountID)
);
