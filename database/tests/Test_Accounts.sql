SET XACT_ABORT OFF; BEGIN TRANSACTION;
BEGIN TRY
    INSERT Accounts (WorkerID,FullName,Email,PasswordHash) VALUES (N'__DBT_A1',N'DB Test Account',N'__dbt_a1@example.invalid',N'not-a-real-login-hash');
    DECLARE @AccountID INT=SCOPE_IDENTITY();
    IF NOT EXISTS (SELECT 1 FROM Accounts WHERE AccountID=@AccountID AND IsActive=1) THROW 51000, 'Account defaults failed.', 1;
    UPDATE Accounts SET IsActive=0 WHERE AccountID=@AccountID;
    IF EXISTS (SELECT 1 FROM Accounts WHERE AccountID=@AccountID AND IsActive<>0) THROW 51000, 'Account deactivation failed.', 1;
    BEGIN TRY
        INSERT Accounts (WorkerID,FullName,Email,PasswordHash) VALUES (N'__DBT_A1',N'Duplicate',N'__dbt_a2@example.invalid',N'x');
        THROW 51001, 'Duplicate WorkerID was accepted.', 1;
    END TRY BEGIN CATCH IF ERROR_NUMBER()=51001 THROW; END CATCH;
    BEGIN TRY
        INSERT Accounts (WorkerID,FullName,Email,PasswordHash) VALUES (N'__DBT_A2',N'Duplicate',N'__dbt_a1@example.invalid',N'x');
        THROW 51002, 'Duplicate Email was accepted.', 1;
    END TRY BEGIN CATCH IF ERROR_NUMBER()=51002 THROW; END CATCH;
    BEGIN TRY
        INSERT Accounts (WorkerID,FullName,Email,PasswordHash) VALUES (N'__DBT_A3',NULL,N'__dbt_a3@example.invalid',N'x');
        THROW 51003, 'NULL FullName was accepted.', 1;
    END TRY BEGIN CATCH IF ERROR_NUMBER()=51003 THROW; END CATCH;
    PRINT 'PASS: accounts'; ROLLBACK;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
