using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MineSafety.Api.Common;
using MineSafety.Api.Dtos;
using MineSafety.Api.Services;

namespace MineSafety.Api.Controllers;

// Photos and voice notes. They are uploaded after the report itself has synced.
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

    // POST /api/reports/5/attachments
    // multipart/form-data with: file, attachmentType, clientAttachmentId, capturedAt
    // 201 = saved, 200 = the same file was already uploaded (a retry), 413 = larger than 25 MB.
    [HttpPost("api/reports/{reportId:int}/attachments")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(AttachmentService.MaxBytes + 1024 * 1024)] // 25 MB plus room for the form fields
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

    // GET /api/attachments/12
    // Returns the actual photo or audio file.
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
