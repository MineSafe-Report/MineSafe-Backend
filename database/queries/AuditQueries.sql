-- Read-only audit diagnostics.
DECLARE @ReportID INT = NULL, @ActorAccountID INT = NULL;

SELECT al.AuditLogID, al.ReportID, rp.ReportNumber, al.ActorAccountID,
       a.WorkerID AS ActorWorkerID, al.Action, al.OldValueJSON, al.NewValueJSON, al.ActionAt
FROM ReportAuditLog AS al
JOIN Reports AS rp ON rp.ReportID = al.ReportID
JOIN Accounts AS a ON a.AccountID = al.ActorAccountID
WHERE (@ReportID IS NULL OR al.ReportID = @ReportID)
  AND (@ActorAccountID IS NULL OR al.ActorAccountID = @ActorAccountID)
ORDER BY al.ActionAt, al.AuditLogID;
