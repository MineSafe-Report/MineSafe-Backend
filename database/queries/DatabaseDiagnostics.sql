-- Read-only deployment, integrity, reference-data, and concurrency diagnostics.
SELECT t.name AS TableName FROM sys.tables AS t
WHERE t.name IN (N'Roles',N'IncidentTypes',N'ReportStatuses',N'Locations',N'Accounts',
                 N'AccountRoles',N'AccountLocations',N'Reports',N'ReportAttachments',
                 N'ReportReviews',N'ReportAuditLog',N'Notifications')
ORDER BY t.name;

SELECT OBJECT_NAME(i.object_id) AS TableName, i.name AS IndexName, i.is_unique, i.has_filter, i.filter_definition
FROM sys.indexes AS i
WHERE OBJECT_NAME(i.object_id) IN (N'Accounts',N'AccountRoles',N'AccountLocations',N'Reports')
  AND i.name IS NOT NULL ORDER BY TableName, IndexName;

SELECT N'Role' AS ReferenceType, RoleName AS ReferenceValue FROM Roles
UNION ALL SELECT N'IncidentType', TypeName FROM IncidentTypes
UNION ALL SELECT N'ReportStatus', StatusName FROM ReportStatuses
ORDER BY ReferenceType, ReferenceValue;

SELECT N'AccountRoles.AccountID' AS CheckName, COUNT(*) AS OrphanCount FROM AccountRoles ar LEFT JOIN Accounts a ON a.AccountID=ar.AccountID WHERE a.AccountID IS NULL
UNION ALL SELECT N'AccountRoles.RoleID', COUNT(*) FROM AccountRoles ar LEFT JOIN Roles r ON r.RoleID=ar.RoleID WHERE r.RoleID IS NULL
UNION ALL SELECT N'AccountLocations.AccountID', COUNT(*) FROM AccountLocations al LEFT JOIN Accounts a ON a.AccountID=al.AccountID WHERE a.AccountID IS NULL
UNION ALL SELECT N'AccountLocations.LocationID', COUNT(*) FROM AccountLocations al LEFT JOIN Locations l ON l.LocationID=al.LocationID WHERE l.LocationID IS NULL
UNION ALL SELECT N'Reports.ReporterAccountID', COUNT(*) FROM Reports rp LEFT JOIN Accounts a ON a.AccountID=rp.ReporterAccountID WHERE a.AccountID IS NULL;

SELECT AccountID, WorkerID, RowVersion FROM Accounts ORDER BY AccountID;
SELECT ReportID, ReportNumber, RowVersion FROM Reports ORDER BY ReportID;
