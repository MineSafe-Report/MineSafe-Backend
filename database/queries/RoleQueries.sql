-- Read-only role assignment diagnostics.
SELECT r.RoleID, r.RoleName, r.Description,
       COUNT(CASE WHEN ar.IsActive = 1 THEN 1 END) AS ActiveAssignments,
       COUNT(CASE WHEN ar.IsActive = 0 THEN 1 END) AS InactiveAssignments
FROM Roles AS r
LEFT JOIN AccountRoles AS ar ON ar.RoleID = r.RoleID
GROUP BY r.RoleID, r.RoleName, r.Description
ORDER BY r.RoleName;

SELECT ar.AccountRoleID, a.WorkerID, r.RoleName, ar.AssignedAt,
       assigner.WorkerID AS AssignedByWorkerID, ar.IsActive
FROM AccountRoles AS ar
JOIN Accounts AS a ON a.AccountID = ar.AccountID
JOIN Roles AS r ON r.RoleID = ar.RoleID
LEFT JOIN Accounts AS assigner ON assigner.AccountID = ar.AssignedBy
ORDER BY a.WorkerID, r.RoleName;

SELECT AccountID, RoleID, COUNT(*) AS AssignmentCount
FROM AccountRoles
GROUP BY AccountID, RoleID
HAVING COUNT(*) > 1;
