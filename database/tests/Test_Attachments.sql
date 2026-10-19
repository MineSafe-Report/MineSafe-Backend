SET XACT_ABORT OFF; BEGIN TRANSACTION;
BEGIN TRY
    DECLARE @R INT=(SELECT TOP(1) ReportID FROM Reports ORDER BY ReportID), @A INT=(SELECT TOP(1) AccountID FROM Accounts ORDER BY AccountID);
    IF @R IS NULL OR @A IS NULL BEGIN PRINT 'SKIP: attachments require an existing report and account.'; ROLLBACK; RETURN; END;
    INSERT ReportAttachments(ReportID,AttachmentType,OriginalFileName,StorageKey,MimeType,SizeBytes,Sha256Hash,UploadedByAccount) VALUES(@R,N'PHOTO',N'test.jpg',N'__dbt_valid',N'image/jpeg',0,REPLICATE('a',64),@A);
    BEGIN TRY INSERT ReportAttachments(ReportID,AttachmentType,OriginalFileName,StorageKey,MimeType,SizeBytes,UploadedByAccount) VALUES(@R,N'PHOTO',N'x',N'__dbt_negative',N'image/jpeg',-1,@A); THROW 51001,'Negative size accepted.',1; END TRY BEGIN CATCH IF ERROR_NUMBER()=51001 THROW; END CATCH;
    BEGIN TRY INSERT ReportAttachments(ReportID,AttachmentType,OriginalFileName,StorageKey,MimeType,SizeBytes,Sha256Hash,UploadedByAccount) VALUES(@R,N'PHOTO',N'x',N'__dbt_hash',N'image/jpeg',1,REPLICATE('z',64),@A); THROW 51002,'Invalid SHA-256 accepted.',1; END TRY BEGIN CATCH IF ERROR_NUMBER()=51002 THROW; END CATCH;
    PRINT 'PASS: attachments'; ROLLBACK;
END TRY BEGIN CATCH IF XACT_STATE()<>0 ROLLBACK; THROW; END CATCH;
