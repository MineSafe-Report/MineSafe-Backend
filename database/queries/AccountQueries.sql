-- Read-only account and assignment diagnostics.
SELECT AccountID, WorkerID, FullName, Email, IsActive, CreatedAt, UpdatedAt, RowVersion
FROM Accounts ORDER BY FullName;

SELECT a.AccountID, a.WorkerID, a.FullName, a.IsActive AS AccountIsActive,
       r.RoleName, ar.IsActive AS RoleIsActive, ar.AssignedAt, ar.AssignedBy
FROM Accounts AS a
LEFT JOIN AccountRoles AS ar ON ar.AccountID = a.AccountID
LEFT JOIN Roles AS r ON r.RoleID = ar.RoleID
ORDER BY a.FullName, r.RoleName;

SELECT a.AccountID, a.WorkerID, a.FullName
FROM Accounts AS a
WHERE NOT EXISTS (SELECT 1 FROM AccountRoles AS ar WHERE ar.AccountID = a.AccountID AND ar.IsActive = 1)
ORDER BY a.FullName;

SELECT a.AccountID, a.WorkerID, a.FullName
FROM Accounts AS a
WHERE a.IsActive = 1
  AND NOT EXISTS (SELECT 1 FROM AccountLocations AS al WHERE al.AccountID = a.AccountID AND al.IsActive = 1)
ORDER BY a.FullName;
