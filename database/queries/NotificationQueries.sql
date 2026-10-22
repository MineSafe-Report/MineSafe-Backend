DECLARE @RecipientAccountID INT = NULL, @ReportID INT = NULL;

SELECT n.NotificationID, n.RecipientAccountID, a.WorkerID, n.ReportID, rp.ReportNumber, n.NotificationType, n.Message, n.CreatedAt, n.ReadAt
FROM Notifications AS n JOIN Accounts AS a ON a.AccountID = n.RecipientAccountID JOIN Reports AS rp ON rp.ReportID = n.ReportID WHERE (@RecipientAccountID IS NULL OR n.RecipientAccountID = @RecipientAccountID) AND (@ReportID IS NULL OR n.ReportID = @ReportID) ORDER BY n.CreatedAt DESC;

SELECT n.* FROM Notifications AS n WHERE n.ReadAt IS NULL AND (@RecipientAccountID IS NULL OR n.RecipientAccountID = @RecipientAccountID) ORDER BY n.CreatedAt DESC;
