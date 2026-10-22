CREATE TABLE ReportStatuses (
    StatusID INT IDENTITY(1,1) NOT NULL,
    StatusName NVARCHAR(50) NOT NULL,
    IsTerminal BIT NOT NULL CONSTRAINT DF_ReportStatuses_IsTerminal DEFAULT (0),
    DisplayOrder INT NOT NULL,

    CONSTRAINT PK_ReportStatuses PRIMARY KEY (StatusID),
    CONSTRAINT UQ_ReportStatuses_StatusName UNIQUE (StatusName),
    CONSTRAINT UQ_ReportStatuses_DisplayOrder UNIQUE (DisplayOrder), CONSTRAINT CK_ReportStatuses_DisplayOrder CHECK (DisplayOrder >= 0)
);
