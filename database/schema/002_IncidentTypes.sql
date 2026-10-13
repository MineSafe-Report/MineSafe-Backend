CREATE TABLE IncidentTypes (
    IncidentTypeID INT IDENTITY(1,1) NOT NULL,
    TypeName NVARCHAR(50) NOT NULL,
    TriggersImmediateAlert BIT NOT NULL DEFAULT (0),
    IsActive BIT NOT NULL DEFAULT (1),

    CONSTRAINT PK_IncidentTypes PRIMARY KEY (IncidentTypeID)
);
