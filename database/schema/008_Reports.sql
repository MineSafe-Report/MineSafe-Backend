CREATE TABLE Reports (
    ReportID INT IDENTITY(1,1) NOT NULL,
    ReportNumber NVARCHAR(50) NOT NULL,
    ReporterAccountID INT NOT NULL,
    IncidentTypeID INT NOT NULL,
    LocationID INT NOT NULL,
    StatusID INT NOT NULL,
    Description NVARCHAR(MAX) NOT NULL,
    IsActive BIT NOT NULL
        CONSTRAINT DF_Reports_IsActive DEFAULT (1),
    CreatedAt DATETIME2(0) NOT NULL
        CONSTRAINT DF_Reports_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt DATETIME2(0) NULL,
    SubmittedAt DATETIME2(0) NULL,
    RowVersion ROWVERSION NOT NULL,

    CONSTRAINT PK_Reports PRIMARY KEY (ReportID),
    CONSTRAINT UQ_Reports_ReportNumber UNIQUE (ReportNumber),
    CONSTRAINT FK_Reports_Accounts FOREIGN KEY (ReporterAccountID) REFERENCES Accounts(AccountID),
    CONSTRAINT FK_Reports_IncidentTypes FOREIGN KEY (IncidentTypeID) REFERENCES IncidentTypes(IncidentTypeID),
    CONSTRAINT FK_Reports_Locations FOREIGN KEY (LocationID) REFERENCES Locations(LocationID),
    CONSTRAINT FK_Reports_ReportStatuses FOREIGN KEY (StatusID) REFERENCES ReportStatuses(StatusID)
);

CREATE INDEX IX_Reports_ReporterAccountID
    ON Reports (ReporterAccountID);

CREATE INDEX IX_Reports_IncidentTypeID
    ON Reports (IncidentTypeID);

CREATE INDEX IX_Reports_LocationID
    ON Reports (LocationID);

CREATE INDEX IX_Reports_StatusID
    ON Reports (StatusID);

CREATE INDEX IX_Reports_CreatedAt
    ON Reports (CreatedAt);
