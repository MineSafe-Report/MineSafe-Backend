SET XACT_ABORT OFF; BEGIN TRANSACTION;
BEGIN TRY
    INSERT Accounts(WorkerID,FullName,Email,PasswordHash) VALUES(N'__DBT_C1',N'Concurrency Test',N'__dbt_c1@example.invalid',N'x'); DECLARE @A INT=SCOPE_IDENTITY(), @Before VARBINARY(8), @After VARBINARY(8);
    SELECT @Before=RowVersion FROM Accounts WHERE AccountID=@A; UPDATE Accounts SET FullName=N'Concurrency Test Updated' WHERE AccountID=@A; SELECT @After=RowVersion FROM Accounts WHERE AccountID=@A;
    IF @Before=@After THROW 51000,'Accounts.RowVersion did not change.',1;
    IF COL_LENGTH(N'Reports',N'RowVersion') IS NULL THROW 51000,'Reports.RowVersion is missing.',1;
    PRINT 'PASS: concurrency'; ROLLBACK;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
