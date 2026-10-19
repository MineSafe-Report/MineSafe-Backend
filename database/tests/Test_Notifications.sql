SET XACT_ABORT OFF; BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @R INT=(SELECT TOP(1) ReportID FROM Reports ORDER BY ReportID), @A INT=(SELECT TOP(1) AccountID FROM Accounts ORDER BY AccountID);
    IF @R IS NULL OR @A IS NULL BEGIN PRINT 'SKIP: notification test requires an existing report and account.'; ROLLBACK; RETURN; END;
    INSERT Notifications(RecipientAccountID,ReportID,NotificationType,Message) VALUES(@A,@R,N'NEW_REPORT',N'Database test'); DECLARE @N INT=SCOPE_IDENTITY();
    IF EXISTS(SELECT 1 FROM Notifications WHERE NotificationID=@N AND ReadAt IS NOT NULL) THROW 51000,'New notification was already read.',1;
    UPDATE Notifications SET ReadAt=SYSUTCDATETIME() WHERE NotificationID=@N;
    IF NOT EXISTS(SELECT 1 FROM Notifications WHERE NotificationID=@N AND ReadAt IS NOT NULL) THROW 51000,'ReadAt update failed.',1;
    PRINT 'PASS: notifications'; ROLLBACK;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
