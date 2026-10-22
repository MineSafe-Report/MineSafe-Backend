using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MineSafety.Api.Common;
using MineSafety.Api.Dtos;
using MineSafety.Api.Services;

namespace MineSafety.Api.Controllers;

// Photos and voice notes: upload after the report is synced, download to view.
[Authorize]
public class AttachmentsController : ApiControllerBase
{
    private readonly IAttachmentService _attachments;
    private readonly IAccessService _access;

    public AttachmentsController(IAttachmentService attachments, IAccessService access)
    {
        _attachments = attachments;
        _access = access;
    }

    // POST /api/reports/5/attachments    (multipart/form-data: file, attachmentType, clientAttachmentId, capturedAt)
    // 201 = saved. 200 = this exact file was already uploaded (a retry). 413 = over 25 MB.
    [HttpPost("api/reports/{reportId:int}/attachments")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(AttachmentService.MaxBytes + 1024 * 1024)]   // 25 MB + room for the form fields
    public async Task<IActionResult> Upload(int reportId, [FromForm] UploadAttachmentRequest request)
    {
        var user = await _access.GetCurrentUserAsync(User);
        var result = await _attachments.UploadAsync(reportId, request, user);
        if (!result.Success)
            return ErrorResponse(result);

        return result.Value!.AlreadyExisted
            ? Ok(result.Value)
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    // GET /api/attachments/12    -> the actual photo / audio file
    [HttpGet("api/attachments/{id:int}")]
    public async Task<IActionResult> Download(int id)
    {
        var user = await _access.GetCurrentUserAsync(User);
        var result = await _attachments.GetFileAsync(id, user);
        if (!result.Success)
            return ErrorResponse(result);

        var (path, mime, fileName) = result.Value;
        return PhysicalFile(path, mime, fileName);
    }
}
