CREATE TABLE AccountRoles (
    AccountRoleID INT IDENTITY(1,1) NOT NULL,
    AccountID INT NOT NULL,
    RoleID INT NOT NULL,
    AssignedAt DATETIME2(0) NOT NULL CONSTRAINT DF_AccountRoles_AssignedAt DEFAULT (SYSUTCDATETIME()),
    AssignedBy INT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_AccountRoles_IsActive DEFAULT (1),

    CONSTRAINT PK_AccountRoles PRIMARY KEY (AccountRoleID),
    CONSTRAINT FK_AccountRoles_Accounts FOREIGN KEY (AccountID) REFERENCES Accounts(AccountID),
    CONSTRAINT FK_AccountRoles_Roles FOREIGN KEY (RoleID) REFERENCES Roles(RoleID),
    CONSTRAINT FK_AccountRoles_AssignedBy_Accounts FOREIGN KEY (AssignedBy) REFERENCES Accounts(AccountID),
    CONSTRAINT UQ_AccountRoles_AccountID_RoleID UNIQUE (AccountID, RoleID)
);

CREATE INDEX IX_AccountRoles_RoleID ON AccountRoles (RoleID);

CREATE INDEX IX_AccountRoles_AssignedBy ON AccountRoles (AssignedBy);
