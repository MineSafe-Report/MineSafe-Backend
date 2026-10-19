SET XACT_ABORT OFF; BEGIN TRANSACTION;
BEGIN TRY
    INSERT Accounts(WorkerID,FullName,Email,PasswordHash) VALUES(N'__DBT_P1',N'Report Test',N'__dbt_p1@example.invalid',N'x'); DECLARE @A INT=SCOPE_IDENTITY();
    INSERT Locations(LocationName,LocationCode) VALUES(N'DB Report Location',N'__DBT_P1'); DECLARE @L INT=SCOPE_IDENTITY();
    DECLARE @I INT=(SELECT IncidentTypeID FROM IncidentTypes WHERE TypeName=N'Hazard'), @S INT=(SELECT StatusID FROM ReportStatuses WHERE StatusName=N'Pending Review'), @C UNIQUEIDENTIFIER=NEWID();
    IF @I IS NULL OR @S IS NULL THROW 51000,'Reference seeds required.',1;
    INSERT Reports(ReportNumber,ClientReportId,ReporterAccountID,IncidentTypeID,LocationID,StatusID,Description,OccurredAt) VALUES(N'__DBT-P1',@C,@A,@I,@L,@S,N'Test',SYSUTCDATETIME());
    DECLARE @R INT=SCOPE_IDENTITY(), @Before VARBINARY(8), @After VARBINARY(8);
    SELECT @Before=RowVersion FROM Reports WHERE ReportID=@R;
    UPDATE Reports SET IsActive=0 WHERE ReportID=@R;
    SELECT @After=RowVersion FROM Reports WHERE ReportID=@R;
    IF @Before=@After THROW 51000,'Reports.RowVersion did not change.',1;
    BEGIN TRY INSERT Reports(ReportNumber,ClientReportId,ReporterAccountID,IncidentTypeID,LocationID,StatusID,Description,OccurredAt) VALUES(N'__DBT-P2',@C,@A,@I,@L,@S,N'Duplicate client id',SYSUTCDATETIME()); THROW 51001,'Duplicate ClientReportId accepted.',1; END TRY BEGIN CATCH IF ERROR_NUMBER()=51001 THROW; END CATCH;
    BEGIN TRY INSERT Reports(ReportNumber,ClientReportId,ReporterAccountID,IncidentTypeID,LocationID,StatusID,Description,OccurredAt) VALUES(N'__DBT-P1',NEWID(),@A,@I,@L,@S,N'Duplicate number',SYSUTCDATETIME()); THROW 51002,'Duplicate ReportNumber accepted.',1; END TRY BEGIN CATCH IF ERROR_NUMBER()=51002 THROW; END CATCH;
    BEGIN TRY INSERT Reports(ReportNumber,ClientReportId,ReporterAccountID,IncidentTypeID,LocationID,StatusID,Description,OccurredAt) VALUES(N'__DBT-P3',NEWID(),-2147483648,@I,@L,@S,N'Invalid reporter',SYSUTCDATETIME()); THROW 51003,'Invalid ReporterAccountID accepted.',1; END TRY BEGIN CATCH IF ERROR_NUMBER()=51003 THROW; END CATCH;
    PRINT 'PASS: reports'; ROLLBACK;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
