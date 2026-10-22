using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Maps to the Notifications table (database/schema/012_Notifications.sql): the alerts shown in the app.
public class Notification
{
    public int NotificationId { get; set; }        // NotificationID (primary key)

    public int RecipientAccountId { get; set; }
    public Account Recipient { get; set; } = null!;

    public int ReportId { get; set; }              // required in SQL, every alert is about a report
    public Report Report { get; set; } = null!;

    [MaxLength(50)]
    public string NotificationType { get; set; } = ""; // INCIDENT_ALERT, REPORT_REVIEWED, ...

    [MaxLength(500)]
    public string Message { get; set; } = "";

    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }          // null means unread
}
