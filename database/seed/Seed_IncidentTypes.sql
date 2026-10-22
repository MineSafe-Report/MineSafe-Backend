SET NOCOUNT ON;

INSERT INTO IncidentTypes (TypeName, TriggersImmediateAlert, IsActive)
SELECT v.TypeName, v.TriggersImmediateAlert, CAST(1 AS BIT)
FROM (VALUES
    (N'Hazard',   CAST(0 AS BIT)), (N'Incident', CAST(1 AS BIT)), (N'Near Miss',CAST(0 AS BIT))
) AS v(TypeName, TriggersImmediateAlert) WHERE NOT EXISTS (
    SELECT 1 FROM IncidentTypes AS t WHERE t.TypeName = v.TypeName );
