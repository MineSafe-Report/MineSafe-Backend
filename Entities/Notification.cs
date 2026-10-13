using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Matches the "Notifications" table: alerts shown in the app.
public class Notification
{
    public int NotificationId { get; set; }

    public int RecipientAccountId { get; set; }
    public Account Recipient { get; set; } = null!;

    public int? ReportId { get; set; }
    public Report? Report { get; set; }

    [MaxLength(50)]
    public string NotificationType { get; set; } = ""; // INCIDENT_ALERT, REPORT_REVIEWED ...

    [MaxLength(500)]
    public string Message { get; set; } = "";

    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }             // null = unread
}
