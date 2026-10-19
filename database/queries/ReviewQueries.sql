-- Read-only review diagnostics.
DECLARE @ReportID INT = NULL, @ReviewerAccountID INT = NULL;

SELECT rv.ReviewID, rv.ReportID, rp.ReportNumber, rv.ReviewerAccountID,
       a.WorkerID AS ReviewerWorkerID, rv.Decision, rv.ReviewComment, rv.ReviewedAt
FROM ReportReviews AS rv
JOIN Reports AS rp ON rp.ReportID = rv.ReportID
JOIN Accounts AS a ON a.AccountID = rv.ReviewerAccountID
WHERE (@ReportID IS NULL OR rv.ReportID = @ReportID)
  AND (@ReviewerAccountID IS NULL OR rv.ReviewerAccountID = @ReviewerAccountID)
ORDER BY rv.ReviewedAt, rv.ReviewID;

SELECT ReportID, COUNT(*) AS FinalReviewCount
FROM ReportReviews
WHERE Decision IN (N'APPROVED', N'DENIED')
GROUP BY ReportID HAVING COUNT(*) > 1;
