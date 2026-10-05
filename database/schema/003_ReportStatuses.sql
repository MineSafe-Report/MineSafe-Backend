CREATE TABLE ReportStatuses (
    StatusID INT IDENTITY(1,1) NOT NULL,
    StatusName NVARCHAR(50) NOT NULL,
    IsTerminal BIT NOT NULL DEFAULT (0),
    DisplayOrder INT NOT NULL,

    CONSTRAINT PK_ReportStatuses PRIMARY KEY (StatusID)
);
