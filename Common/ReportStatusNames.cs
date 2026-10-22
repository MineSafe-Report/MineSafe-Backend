namespace MineSafety.Api.Common;

// The StatusName values in the ReportStatuses table, in ONE place.
public static class ReportStatusNames
{
    public const string PendingReview = "Pending Review";
    public const string Approved = "Approved";
    public const string Denied = "Denied";
}

// The values written to ReportReviews.Decision
public static class ReviewDecisions
{
    public const string Approved = "APPROVED";
    public const string Denied = "DENIED";

    // The apps may send APPROVE / DENY (their doc) or APPROVED / DENIED. Both work.
    public static string? Normalise(string? decision) => decision?.Trim().ToUpperInvariant() switch
    {
        "APPROVE" or "APPROVED" => Approved,
        "DENY" or "DENIED" => Denied,
        _ => null
    };
}

// The values written to Notifications.NotificationType
public static class NotificationTypes
{
    public const string IncidentAlert = "INCIDENT_ALERT";   // urgent: an Incident was reported
    public const string NewReport = "NEW_REPORT";           // a normal new report in your location
    public const string ReportReviewed = "REPORT_REVIEWED"; // your report was approved or denied
    public const string IncidentApproved = "INCIDENT_APPROVED"; // Safety Officers: an Incident was approved
}
