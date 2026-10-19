SET NOCOUNT ON;
DECLARE @Expected TABLE(Name SYSNAME PRIMARY KEY);
INSERT @Expected VALUES(N'Roles'),(N'IncidentTypes'),(N'ReportStatuses'),(N'Locations'),(N'Accounts'),(N'AccountRoles'),(N'AccountLocations'),(N'Reports'),(N'ReportAttachments'),(N'ReportReviews'),(N'ReportAuditLog'),(N'Notifications');
IF EXISTS(SELECT 1 FROM @Expected e WHERE OBJECT_ID(N'dbo.'+e.Name,N'U') IS NULL) THROW 51000,'One or more required dbo tables are missing.',1;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.AccountLocations') AND name=N'UX_AccountLocations_OneActiveDefaultPerAccount' AND is_unique=1 AND has_filter=1) THROW 51000,'Active-default filtered unique index is missing.',1;
IF NOT EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.Reports') AND name=N'UQ_Reports_ClientReportId') THROW 51000,'ClientReportId unique constraint is missing.',1;
IF NOT EXISTS(SELECT 1 FROM sys.key_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.Reports') AND name=N'UQ_Reports_ReportNumber') THROW 51000,'ReportNumber unique constraint is missing.',1;
IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE delete_referential_action<>0 AND parent_object_id IN (SELECT object_id FROM sys.tables WHERE schema_id=SCHEMA_ID(N'dbo'))) THROW 51000,'Unexpected cascading delete found.',1;
PRINT 'PASS: clean deployment structure';
