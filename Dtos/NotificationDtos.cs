namespace MineSafety.Api.Dtos;

public class NotificationDto
{
    public int NotificationId { get; set; }
    public string NotificationType { get; set; } = "";   // INCIDENT_ALERT, NEW_REPORT, REPORT_REVIEWED
    public string Message { get; set; } = "";
    public int ReportId { get; set; }                    // tap the alert = open this report
    public string ReportNumber { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }                // null = unread
}
