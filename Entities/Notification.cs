using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Matches database/schema/012_Notifications.sql : alerts shown in the app.
public class Notification
{
    public int NotificationId { get; set; }          // NotificationID (PK)

    public int RecipientAccountId { get; set; }
    public Account Recipient { get; set; } = null!;

    public int ReportId { get; set; }                // NOT NULL in SQL: every alert is about a report
    public Report Report { get; set; } = null!;

    [MaxLength(50)]
    public string NotificationType { get; set; } = ""; // INCIDENT_ALERT, REPORT_REVIEWED ...

    [MaxLength(500)]
    public string Message { get; set; } = "";

    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }            // null = unread
}
