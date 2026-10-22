DECLARE @LocationID INT = NULL, @StatusID INT = NULL, @ReporterAccountID INT = NULL;
DECLARE @FromDate DATETIME2(0) = NULL, @ToDate DATETIME2(0) = NULL;

SELECT rp.ReportID, rp.ReportNumber, rp.ClientReportId, a.WorkerID, a.FullName AS Reporter, it.TypeName, l.LocationCode, l.LocationName, rs.StatusName, rp.Description, rp.OccurredAt, rp.SubmittedAt, rp.CreatedAt, rp.UpdatedAt, rp.IsActive, rp.RowVersion
FROM Reports AS rp JOIN Accounts AS a ON a.AccountID = rp.ReporterAccountID JOIN IncidentTypes AS it ON it.IncidentTypeID = rp.IncidentTypeID JOIN Locations AS l ON l.LocationID = rp.LocationID JOIN ReportStatuses AS rs ON rs.StatusID = rp.StatusID WHERE (@LocationID IS NULL OR rp.LocationID = @LocationID) AND (@StatusID IS NULL OR rp.StatusID = @StatusID) AND (@ReporterAccountID IS NULL OR rp.ReporterAccountID = @ReporterAccountID) AND (@FromDate IS NULL OR rp.OccurredAt >= @FromDate) AND (@ToDate IS NULL OR rp.OccurredAt < @ToDate) ORDER BY rp.CreatedAt DESC;

SELECT ClientReportId, COUNT(*) AS DuplicateCount FROM Reports GROUP BY ClientReportId HAVING COUNT(*) > 1;

SELECT ReportNumber, COUNT(*) AS DuplicateCount FROM Reports GROUP BY ReportNumber HAVING COUNT(*) > 1;
