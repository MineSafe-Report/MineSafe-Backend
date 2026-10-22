namespace MineSafety.Api.Dtos;

// A notification as shown on the alerts screen.
public class NotificationDto
{
    public int NotificationId { get; set; }
    public string NotificationType { get; set; } = ""; // INCIDENT_ALERT, NEW_REPORT, REPORT_REVIEWED or INCIDENT_APPROVED
    public string Message { get; set; } = "";
    public int ReportId { get; set; }                  // the report that opens when the alert is tapped
    public string ReportNumber { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }              // null means unread
}
