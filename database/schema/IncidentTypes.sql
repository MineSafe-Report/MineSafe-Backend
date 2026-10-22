CREATE TABLE IncidentTypes (
    IncidentTypeID INT IDENTITY(1,1) NOT NULL,
    TypeName NVARCHAR(50) NOT NULL,
    TriggersImmediateAlert BIT NOT NULL CONSTRAINT DF_IncidentTypes_TriggersImmediateAlert DEFAULT (0),
    IsActive BIT NOT NULL CONSTRAINT DF_IncidentTypes_IsActive DEFAULT (1),

    CONSTRAINT PK_IncidentTypes PRIMARY KEY (IncidentTypeID),
    CONSTRAINT UQ_IncidentTypes_TypeName UNIQUE (TypeName)
);
