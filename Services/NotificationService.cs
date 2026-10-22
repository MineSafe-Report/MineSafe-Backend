using Microsoft.EntityFrameworkCore;
using MineSafety.Api.Common;
using MineSafety.Api.Data;
using MineSafety.Api.Dtos;

namespace MineSafety.Api.Services;

public interface INotificationService
{
    Task<PagedResult<NotificationDto>> GetMineAsync(int accountId, bool unreadOnly, int page, int pageSize);
    Task<ServiceResult<NotificationDto>> MarkReadAsync(int notificationId, int accountId);
    Task<int> MarkAllReadAsync(int accountId);
}

// Reading notifications and marking them as read. Users only ever see their own notifications.
// The notifications themselves are created by ReportService, in the same transaction as the
// report or review they are about.
public class NotificationService : INotificationService
{
    private readonly MineSafetyDbContext _db;

    public NotificationService(MineSafetyDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<NotificationDto>> GetMineAsync(int accountId, bool unreadOnly, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Notifications.AsNoTracking().Where(n => n.RecipientAccountId == accountId);
        if (unreadOnly)
            query = query.Where(n => n.ReadAt == null);

        var total = await query.CountAsync();

        // Unread first, then urgent alerts, then newest first.
        var items = await query
            .Include(n => n.Report)
            .OrderBy(n => n.ReadAt != null)
            .ThenByDescending(n => n.NotificationType == NotificationTypes.IncidentAlert)
            .ThenByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<NotificationDto>
        {
            Items = items.Select(n => new NotificationDto
            {
                NotificationId = n.NotificationId,
                NotificationType = n.NotificationType,
                Message = n.Message,
                ReportId = n.ReportId,
                ReportNumber = n.Report.ReportNumber,
                CreatedAt = n.CreatedAt,
                ReadAt = n.ReadAt
            }).ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total
        };
    }

    public async Task<ServiceResult<NotificationDto>> MarkReadAsync(int notificationId, int accountId)
    {
        // Checking the recipient means someone else's notification returns 404.
        var n = await _db.Notifications.Include(x => x.Report)
            .FirstOrDefaultAsync(x => x.NotificationId == notificationId && x.RecipientAccountId == accountId);
        if (n == null)
            return ServiceResult<NotificationDto>.NotFound($"Notification {notificationId} was not found.");

        n.ReadAt ??= DateTime.UtcNow; // only set the first time it is read
        await _db.SaveChangesAsync();

        return ServiceResult<NotificationDto>.Ok(new NotificationDto
        {
            NotificationId = n.NotificationId,
            NotificationType = n.NotificationType,
            Message = n.Message,
            ReportId = n.ReportId,
            ReportNumber = n.Report.ReportNumber,
            CreatedAt = n.CreatedAt,
            ReadAt = n.ReadAt
        });
    }

    public async Task<int> MarkAllReadAsync(int accountId)
    {
        var unread = await _db.Notifications
            .Where(n => n.RecipientAccountId == accountId && n.ReadAt == null)
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var n in unread)
            n.ReadAt = now;

        await _db.SaveChangesAsync();
        return unread.Count;
    }
}
