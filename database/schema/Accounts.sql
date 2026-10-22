CREATE TABLE Accounts (
    AccountID INT IDENTITY(1,1) NOT NULL,
    WorkerID NVARCHAR(50) NOT NULL,
    FullName NVARCHAR(100) NOT NULL,
    Email NVARCHAR(255) NOT NULL,
    PasswordHash NVARCHAR(255) NOT NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Accounts_IsActive DEFAULT (1),
    CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_Accounts_CreatedAt DEFAULT (SYSUTCDATETIME()),
    UpdatedAt DATETIME2(0) NULL,
    RowVersion ROWVERSION NOT NULL,

    CONSTRAINT PK_Accounts PRIMARY KEY (AccountID),
    CONSTRAINT UQ_Accounts_WorkerID UNIQUE (WorkerID),
    CONSTRAINT UQ_Accounts_Email UNIQUE (Email)
);

CREATE INDEX IX_Accounts_FullName ON Accounts (FullName);
