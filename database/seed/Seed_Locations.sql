-- DEVELOPMENT DATA ONLY. These locations match the existing application development
-- seeder and are not approved production mine locations. Replace or omit them during
-- production deployment until official location values are supplied.
SET NOCOUNT ON;

INSERT INTO Locations (LocationName, LocationCode, LocationInfo, IsActive)
SELECT v.LocationName, v.LocationCode, v.LocationInfo, CAST(1 AS BIT)
FROM (VALUES
    (N'Shaft 1 Station', N'S1-ST',  N'Main shaft landing, level 1'),
    (N'Tunnel 7',        N'T-7',    N'Haulage tunnel, level 3'),
    (N'Stope 12B',       N'ST-12B', N'Active stope, level 4'),
    (N'Workshop',        N'WS-1',   N'Underground vehicle workshop')
) AS v(LocationName, LocationCode, LocationInfo)
WHERE NOT EXISTS (
    SELECT 1 FROM Locations AS l WHERE l.LocationCode = v.LocationCode
);

-- TODO: obtain and approve the official production location codes, names, and details.
