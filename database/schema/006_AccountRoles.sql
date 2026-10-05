CREATE TABLE AccountRoles (
    AccountRoleID INT IDENTITY(1,1) NOT NULL,
    AccountID INT NOT NULL,
    RoleID INT NOT NULL,
    AssignedAt DATETIME2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
    AssignedBy INT NULL,
    IsActive BIT NOT NULL DEFAULT (1),

    CONSTRAINT PK_AccountRoles PRIMARY KEY (AccountRoleID),
    CONSTRAINT FK_AccountRoles_Accounts FOREIGN KEY (AccountID) REFERENCES Accounts(AccountID),
    CONSTRAINT FK_AccountRoles_Roles FOREIGN KEY (RoleID) REFERENCES Roles(RoleID),
    CONSTRAINT FK_AccountRoles_AssignedBy FOREIGN KEY (AssignedBy) REFERENCES Accounts(AccountID)
);
