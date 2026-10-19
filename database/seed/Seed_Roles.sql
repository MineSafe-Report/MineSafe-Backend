-- Idempotent reference data. Names must match Common/RoleNames.cs exactly.
SET NOCOUNT ON;

INSERT INTO Roles (RoleName, Description)
SELECT v.RoleName, v.Description
FROM (VALUES
    (N'Worker',        N'Underground worker who submits reports'),
    (N'Supervisor',    N'Reviews reports for assigned locations'),
    (N'SafetyOfficer', N'Manages reports mine-wide'),
    (N'Administrator', N'Manages accounts and locations')
) AS v(RoleName, Description)
WHERE NOT EXISTS (
    SELECT 1 FROM Roles AS r WHERE r.RoleName = v.RoleName
);
