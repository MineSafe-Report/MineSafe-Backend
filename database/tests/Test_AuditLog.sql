SET XACT_ABORT OFF; BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @R INT=(SELECT TOP(1) ReportID FROM Reports ORDER BY ReportID), @A INT=(SELECT TOP(1) AccountID FROM Accounts ORDER BY AccountID);
    IF @R IS NULL OR @A IS NULL BEGIN PRINT 'SKIP: audit test requires an existing report and account.'; ROLLBACK; RETURN; END;
    INSERT ReportAuditLog(ReportID,ActorAccountID,Action,OldValueJSON,NewValueJSON) VALUES(@R,@A,N'TEST',N'{"old":1}',N'{"new":2}');
    BEGIN TRY INSERT ReportAuditLog(ReportID,ActorAccountID,Action,OldValueJSON) VALUES(@R,@A,N'TEST',N'not-json'); THROW 51001,'Invalid JSON accepted.',1; END TRY BEGIN CATCH IF ERROR_NUMBER()=51001 THROW; END CATCH;
    SELECT AuditLogID,ActionAt FROM ReportAuditLog WHERE ReportID=@R ORDER BY ActionAt,AuditLogID;
    PRINT 'PASS: audit log'; ROLLBACK;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
