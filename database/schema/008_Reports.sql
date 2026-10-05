CREATE TABLE Reports (
    ReportID INT IDENTITY(1,1) NOT NULL,
    ReportNumber NVARCHAR(50) NOT NULL,
    ReporterAccountID INT NOT NULL,
    IncidentTypeID INT NOT NULL,
    LocationID INT NOT NULL,
    StatusID INT NOT NULL,
    Description NVARCHAR(MAX) NOT NULL,
    IsActive BIT NOT NULL DEFAULT (1),
    CreatedAt DATETIME2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    UpdatedAt DATETIME2(0) NULL,
    SubmittedAt DATETIME2(0) NULL,
    RowVersion ROWVERSION NOT NULL,

    CONSTRAINT PK_Reports PRIMARY KEY (ReportID),
    CONSTRAINT FK_Reports_Accounts FOREIGN KEY (ReporterAccountID) REFERENCES Accounts(AccountID),
    CONSTRAINT FK_Reports_IncidentTypes FOREIGN KEY (IncidentTypeID) REFERENCES IncidentTypes(IncidentTypeID),
    CONSTRAINT FK_Reports_Locations FOREIGN KEY (LocationID) REFERENCES Locations(LocationID),
    CONSTRAINT FK_Reports_ReportStatuses FOREIGN KEY (StatusID) REFERENCES ReportStatuses(StatusID)
);
