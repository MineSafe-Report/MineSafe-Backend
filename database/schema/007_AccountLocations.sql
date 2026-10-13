CREATE TABLE AccountLocations (
    AccountLocation INT IDENTITY(1,1) NOT NULL,
    AccountID INT NOT NULL,
    LocationID INT NOT NULL,
    IsDefault BIT NOT NULL DEFAULT (0),
    IsActive BIT NOT NULL DEFAULT (1),
    AssignedAt DATETIME2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    AssignedBy INT NULL,

    CONSTRAINT PK_AccountLocations PRIMARY KEY (AccountLocation),
    CONSTRAINT FK_AccountLocations_Accounts FOREIGN KEY (AccountID) REFERENCES Accounts(AccountID),
    CONSTRAINT FK_AccountLocations_Locations FOREIGN KEY (LocationID) REFERENCES Locations(LocationID),
    CONSTRAINT FK_AccountLocations_AssignedBy FOREIGN KEY (AssignedBy) REFERENCES Accounts(AccountID)
);
