namespace MineSafety.Api.Common;

// The StatusName values from the ReportStatuses table.
public static class ReportStatusNames
{
    public const string PendingReview = "Pending Review";
    public const string Approved = "Approved";
    public const string Denied = "Denied";
}

// The values stored in ReportReviews.Decision.
public static class ReviewDecisions
{
    public const string Approved = "APPROVED";
    public const string Denied = "DENIED";

    // Accepts both APPROVE / DENY and APPROVED / DENIED from the apps.
    public static string? Normalise(string? decision) => decision?.Trim().ToUpperInvariant() switch
    {
        "APPROVE" or "APPROVED" => Approved,
        "DENY" or "DENIED" => Denied,
        _ => null
    };
}

// The values stored in Notifications.NotificationType.
public static class NotificationTypes
{
    public const string IncidentAlert = "INCIDENT_ALERT";       // an incident was reported (urgent)
    public const string NewReport = "NEW_REPORT";               // a new report at one of your locations
    public const string ReportReviewed = "REPORT_REVIEWED";     // your report was approved or denied
    public const string IncidentApproved = "INCIDENT_APPROVED"; // sent to Safety Officers when an incident is approved
}
