-- Idempotent reference data. Names must match Common/ReportStatusNames.cs exactly.
SET NOCOUNT ON;

INSERT INTO ReportStatuses (StatusName, IsTerminal, DisplayOrder)
SELECT v.StatusName, v.IsTerminal, v.DisplayOrder
FROM (VALUES
    (N'Pending Review', CAST(0 AS BIT), 1),
    (N'Approved',       CAST(1 AS BIT), 2),
    (N'Denied',         CAST(1 AS BIT), 3)
) AS v(StatusName, IsTerminal, DisplayOrder)
WHERE NOT EXISTS (
    SELECT 1 FROM ReportStatuses AS s WHERE s.StatusName = v.StatusName
);
