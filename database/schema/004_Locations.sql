CREATE TABLE Locations (
    LocationID INT IDENTITY(1,1) NOT NULL,
    LocationName NVARCHAR(100) NOT NULL,
    LocationCode NVARCHAR(50) NOT NULL,
    LocationInfo NVARCHAR(255) NULL,
    IsActive BIT NOT NULL
        CONSTRAINT DF_Locations_IsActive DEFAULT (1),
    CreatedAt DATETIME2(0) NOT NULL
        CONSTRAINT DF_Locations_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt DATETIME2(0) NULL,

    CONSTRAINT PK_Locations PRIMARY KEY (LocationID),
    CONSTRAINT UQ_Locations_LocationCode UNIQUE (LocationCode)
);

CREATE INDEX IX_Locations_LocationName
    ON Locations (LocationName);
