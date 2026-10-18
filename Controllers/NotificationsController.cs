using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MineSafety.Api.Services;

namespace MineSafety.Api.Controllers;

// Handles /api/notifications : the alerts screen. You only ever see your own.
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ApiControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    // GET /api/notifications?unreadOnly=true&page=1&pageSize=20
    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] bool unreadOnly = false,
                                             [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        return Ok(await _notifications.GetMineAsync(CurrentAccountId, unreadOnly, page, pageSize));
    }

    // PUT /api/notifications/5/read
    [HttpPut("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var result = await _notifications.MarkReadAsync(id, CurrentAccountId);
        return result.Success ? Ok(result.Value) : ErrorResponse(result);
    }

    // PUT /api/notifications/read-all
    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var count = await _notifications.MarkAllReadAsync(CurrentAccountId);
        return Ok(new { markedRead = count });
    }
}
