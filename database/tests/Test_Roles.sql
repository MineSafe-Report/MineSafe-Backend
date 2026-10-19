SET XACT_ABORT OFF; BEGIN TRANSACTION;
BEGIN TRY
    INSERT Accounts (WorkerID,FullName,Email,PasswordHash) VALUES (N'__DBT_R1',N'Role Test',N'__dbt_r1@example.invalid',N'x');
    DECLARE @AccountID INT=SCOPE_IDENTITY(), @RoleID INT=(SELECT RoleID FROM Roles WHERE RoleName=N'Worker');
    IF @RoleID IS NULL THROW 51000, 'Worker seed is required.', 1;
    INSERT AccountRoles(AccountID,RoleID,AssignedBy) VALUES(@AccountID,@RoleID,@AccountID);
    UPDATE AccountRoles SET IsActive=0 WHERE AccountID=@AccountID AND RoleID=@RoleID;
    IF NOT EXISTS(SELECT 1 FROM AccountRoles WHERE AccountID=@AccountID AND RoleID=@RoleID AND IsActive=0) THROW 51000, 'Role deactivation failed.',1;
    BEGIN TRY INSERT AccountRoles(AccountID,RoleID) VALUES(@AccountID,@RoleID); THROW 51001,'Duplicate role assignment accepted.',1; END TRY BEGIN CATCH IF ERROR_NUMBER()=51001 THROW; END CATCH;
    PRINT 'PASS: role assignments'; ROLLBACK;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
