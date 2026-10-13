CREATE TABLE AccountLocations (
    AccountLocation INT IDENTITY(1,1) NOT NULL,
    AccountID INT NOT NULL,
    LocationID INT NOT NULL,
    IsDefault BIT NOT NULL
        CONSTRAINT DF_AccountLocations_IsDefault DEFAULT (0),
    IsActive BIT NOT NULL
        CONSTRAINT DF_AccountLocations_IsActive DEFAULT (1),
    AssignedAt DATETIME2(0) NOT NULL
        CONSTRAINT DF_AccountLocations_AssignedAt DEFAULT (SYSUTCDATETIME()),
    AssignedBy INT NULL,

    CONSTRAINT PK_AccountLocations PRIMARY KEY (AccountLocation),
    CONSTRAINT FK_AccountLocations_Accounts FOREIGN KEY (AccountID) REFERENCES Accounts(AccountID),
    CONSTRAINT FK_AccountLocations_Locations FOREIGN KEY (LocationID) REFERENCES Locations(LocationID),
    CONSTRAINT FK_AccountLocations_AssignedBy_Accounts FOREIGN KEY (AssignedBy) REFERENCES Accounts(AccountID),
    CONSTRAINT UQ_AccountLocations_AccountID_LocationID UNIQUE (AccountID, LocationID)
);

CREATE INDEX IX_AccountLocations_LocationID
    ON AccountLocations (LocationID);

CREATE INDEX IX_AccountLocations_AssignedBy
    ON AccountLocations (AssignedBy);
