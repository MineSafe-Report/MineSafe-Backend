SET XACT_ABORT OFF; BEGIN TRANSACTION;
BEGIN TRY
    INSERT Accounts(WorkerID,FullName,Email,PasswordHash) VALUES(N'__DBT_L1',N'Location Test',N'__dbt_l1@example.invalid',N'x'); DECLARE @A INT=SCOPE_IDENTITY();
    INSERT Locations(LocationName,LocationCode) VALUES(N'DB Test L1',N'__DBT_L1'); DECLARE @L1 INT=SCOPE_IDENTITY();
    INSERT Locations(LocationName,LocationCode) VALUES(N'DB Test L2',N'__DBT_L2'); DECLARE @L2 INT=SCOPE_IDENTITY();
    INSERT AccountLocations(AccountID,LocationID,IsDefault) VALUES(@A,@L1,1);
    INSERT AccountLocations(AccountID,LocationID,IsDefault) VALUES(@A,@L2,0);
    BEGIN TRY UPDATE AccountLocations SET IsDefault=1 WHERE AccountID=@A AND LocationID=@L2; THROW 51001,'Multiple active defaults accepted.',1; END TRY BEGIN CATCH IF ERROR_NUMBER()=51001 THROW; END CATCH;
    UPDATE AccountLocations SET IsActive=0 WHERE AccountID=@A AND LocationID=@L1;
    UPDATE AccountLocations SET IsDefault=1 WHERE AccountID=@A AND LocationID=@L2;
    PRINT 'PASS: location assignments'; ROLLBACK;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
