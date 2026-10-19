SET NOCOUNT ON;
IF (SELECT COUNT(*) FROM Roles WHERE RoleName IN (N'Worker',N'Supervisor',N'SafetyOfficer',N'Administrator')) <> 4 THROW 51000, 'Required roles are missing.', 1;
IF (SELECT COUNT(*) FROM IncidentTypes WHERE TypeName IN (N'Hazard',N'Incident',N'Near Miss')) <> 3 THROW 51000, 'Required incident types are missing.', 1;
IF (SELECT COUNT(*) FROM ReportStatuses WHERE StatusName IN (N'Pending Review',N'Approved',N'Denied')) <> 3 THROW 51000, 'Required report statuses are missing.', 1;
IF EXISTS (SELECT RoleName FROM Roles GROUP BY RoleName HAVING COUNT(*) > 1) THROW 51000, 'Duplicate role name.', 1;
IF EXISTS (SELECT TypeName FROM IncidentTypes GROUP BY TypeName HAVING COUNT(*) > 1) THROW 51000, 'Duplicate incident type.', 1;
IF EXISTS (SELECT StatusName FROM ReportStatuses GROUP BY StatusName HAVING COUNT(*) > 1) THROW 51000, 'Duplicate report status.', 1;
IF EXISTS (SELECT LocationCode FROM Locations GROUP BY LocationCode HAVING COUNT(*) > 1) THROW 51000, 'Duplicate location code.', 1;
PRINT 'PASS: reference data';
