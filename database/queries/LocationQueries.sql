-- Read-only location and location-assignment diagnostics.
SELECT LocationID, LocationCode, LocationName, LocationInfo, IsActive, CreatedAt, UpdatedAt
FROM Locations ORDER BY IsActive DESC, LocationName;

SELECT al.AccountLocation, a.WorkerID, l.LocationCode, l.LocationName,
       al.IsDefault, al.IsActive, al.AssignedAt, assigner.WorkerID AS AssignedByWorkerID
FROM AccountLocations AS al
JOIN Accounts AS a ON a.AccountID = al.AccountID
JOIN Locations AS l ON l.LocationID = al.LocationID
LEFT JOIN Accounts AS assigner ON assigner.AccountID = al.AssignedBy
ORDER BY a.WorkerID, al.IsDefault DESC, l.LocationName;

SELECT AccountID, COUNT(*) AS ActiveDefaultCount
FROM AccountLocations
WHERE IsDefault = 1 AND IsActive = 1
GROUP BY AccountID
HAVING COUNT(*) > 1;
