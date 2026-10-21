using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using MineSafety.Api.Common;
using MineSafety.Api.Data;
using MineSafety.Api.Dtos;
using MineSafety.Api.Entities;

namespace MineSafety.Api.Services;

public interface IAttachmentService
{
    Task<ServiceResult<UploadAttachmentResponse>> UploadAsync(int reportId, UploadAttachmentRequest request, CurrentUser user);
    Task<ServiceResult<(string Path, string MimeType, string FileName)>> GetFileAsync(int attachmentId, CurrentUser user);
}

// Photos and voice notes.
//  - The FILE is saved in a folder on the server (the "object store"), outside the website.
//  - The DATABASE row (ReportAttachments) only stores its name, type, size and SHA-256 hash.
//  - The app never sees the folder path; it downloads through GET /api/attachments/{id},
//    which checks the user may see the report first.
public class AttachmentService : IAttachmentService
{
    public const long MaxBytes = 25 * 1024 * 1024;   // 25 MB, matches the phone's limit

    private static readonly Dictionary<string, string[]> AllowedMimeTypes = new()
    {
        ["PHOTO"] = new[] { "image/jpeg", "image/png", "image/webp", "image/heic" },
        ["VOICE_NOTE"] = new[] { "audio/mp4", "audio/m4a", "audio/x-m4a", "audio/aac", "audio/mpeg",
                                 "audio/3gpp", "audio/amr", "audio/ogg", "audio/wav", "audio/webm" }
    };

    private readonly MineSafetyDbContext _db;
    private readonly IReportService _reports;
    private readonly string _storageRoot;

    public AttachmentService(MineSafetyDbContext db, IReportService reports, IConfiguration config, IWebHostEnvironment env)
    {
        _db = db;
        _reports = reports;
        // Default: a folder called "uploads" next to the project (it's in .gitignore).
        _storageRoot = Path.GetFullPath(Path.Combine(env.ContentRootPath, config["Storage:AttachmentsPath"] ?? "uploads"));
    }

    public async Task<ServiceResult<UploadAttachmentResponse>> UploadAsync(int reportId, UploadAttachmentRequest request, CurrentUser user)
    {
        // 1. The report must exist, be visible to you, and be YOURS.
        var report = await _reports.VisibleTo(user).FirstOrDefaultAsync(r => r.ReportId == reportId);
        if (report == null)
            return ServiceResult<UploadAttachmentResponse>.NotFound($"Report {reportId} was not found.");
        if (report.ReporterAccountId != user.AccountId)
            return ServiceResult<UploadAttachmentResponse>.BadRequest("Only the reporter can add evidence.", "NOT_REPORTER");

        // 2. Check the type and the file.
        var type = request.AttachmentType.Trim().ToUpperInvariant() switch
        {
            "PHOTO" or "IMAGE" => "PHOTO",
            "VOICE_NOTE" or "VOICE" or "AUDIO" => "VOICE_NOTE",
            _ => null
        };
        if (type == null)
            return ServiceResult<UploadAttachmentResponse>.BadRequest("attachmentType must be PHOTO or VOICE_NOTE.", "INVALID_ATTACHMENT_TYPE");

        var file = request.File;
        if (file.Length == 0)
            return ServiceResult<UploadAttachmentResponse>.BadRequest("The file is empty.", "EMPTY_FILE");
        if (file.Length > MaxBytes)
            return ServiceResult<UploadAttachmentResponse>.TooLarge("Files can be at most 25 MB.");

        var mime = (file.ContentType ?? "").ToLowerInvariant();
        if (!AllowedMimeTypes[type].Contains(mime))
            return ServiceResult<UploadAttachmentResponse>.BadRequest(
                $"A {type} can't be of type '{mime}'.", "UNSUPPORTED_FILE_TYPE");

        // 3. Read the file once, working out its SHA-256 "fingerprint" as we go.
        byte[] bytes;
        using (var memory = new MemoryStream())
        {
            await file.CopyToAsync(memory);
            bytes = memory.ToArray();
        }
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        // 4. RETRY SAFETY: already got this upload? Hand the original back (and still run the
        //    auto-approval check, in case the earlier attempt stopped before it).
        //    With a client ID: match on that ID. Without one: match on the file's fingerprint.
        var clientId = (request.ClientAttachmentId ?? request.LocalAttachmentId)?.Trim();
        if (string.IsNullOrEmpty(clientId)) clientId = null;

        var existing = await FindExistingAsync(reportId, clientId, hash);
        if (existing != null)
            return ServiceResult<UploadAttachmentResponse>.Ok(await BuildResponseAsync(existing, alreadyExisted: true, user));

        // 5. Save the file under a name WE choose (never the phone's file name or path).
        var extension = Path.GetExtension(file.FileName);
        if (extension.Length > 10) extension = "";
        var storageKey = $"{reportId}/{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(_storageRoot, storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllBytesAsync(fullPath, bytes);

        // 6. Save the database row + an audit entry together.
        var attachment = new ReportAttachment
        {
            ReportId = reportId,
            AttachmentType = type,
            OriginalFileName = Path.GetFileName(file.FileName).Length > 255
                ? Path.GetFileName(file.FileName)[..255] : Path.GetFileName(file.FileName),
            StorageKey = storageKey,
            MimeType = mime,
            SizeBytes = bytes.LongLength,
            Sha256Hash = hash,
            ClientAttachmentId = clientId,
            UploadedByAccountId = user.AccountId,     // from the token, never from the app
            UploadedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        _db.ReportAttachments.Add(attachment);
        _reports.AddAudit(reportId, user.AccountId, "ATTACHMENT_ADDED", null,
            new { attachmentType = type, mimeType = mime, sizeBytes = bytes.LongLength, sha256 = hash });

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException) when (clientId != null)
        {
            // The same upload arrived twice at the same moment; the database's unique rule on
            // (report, client ID) stopped this copy. Answer like a normal retry.
            File.Delete(fullPath);
            _db.ChangeTracker.Clear();
            var winner = await FindExistingAsync(reportId, clientId, hash);
            if (winner == null) throw;
            return ServiceResult<UploadAttachmentResponse>.Ok(await BuildResponseAsync(winner, alreadyExisted: true, user));
        }
        catch
        {
            File.Delete(fullPath);    // don't leave an orphan file if the database save failed
            throw;
        }

        return ServiceResult<UploadAttachmentResponse>.Ok(await BuildResponseAsync(attachment, alreadyExisted: false, user));
    }

    private Task<ReportAttachment?> FindExistingAsync(int reportId, string? clientId, string hash) =>
        clientId != null
            ? _db.ReportAttachments.AsNoTracking()
                .FirstOrDefaultAsync(a => a.ReportId == reportId && a.ClientAttachmentId == clientId && !a.IsDeleted)
            : _db.ReportAttachments.AsNoTracking()
                .FirstOrDefaultAsync(a => a.ReportId == reportId && a.Sha256Hash == hash && !a.IsDeleted);

    // After every upload (new or repeated): a Supervisor's own report is approved automatically
    // if this was the last expected file. Then report the report's current state back.
    private async Task<UploadAttachmentResponse> BuildResponseAsync(ReportAttachment attachment, bool alreadyExisted, CurrentUser user)
    {
        await _reports.TryAutoApproveAsync(attachment.ReportId, user);
        var (statusCode, evidenceComplete) = await _reports.GetEvidenceStateAsync(attachment.ReportId);
        return new UploadAttachmentResponse
        {
            AlreadyExisted = alreadyExisted,
            Attachment = ToDto(attachment),
            ReportStatusCode = statusCode,
            EvidenceComplete = evidenceComplete
        };
    }

    public async Task<ServiceResult<(string Path, string MimeType, string FileName)>> GetFileAsync(int attachmentId, CurrentUser user)
    {
        // The SAME visibility rule as reports: no access to the report = the file "doesn't exist".
        var visibleReports = _reports.VisibleTo(user).Select(r => r.ReportId);
        var attachment = await _db.ReportAttachments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.AttachmentId == attachmentId && !a.IsDeleted && visibleReports.Contains(a.ReportId));

        if (attachment == null)
            return ServiceResult<(string, string, string)>.NotFound($"Attachment {attachmentId} was not found.");

        var fullPath = Path.GetFullPath(Path.Combine(_storageRoot, attachment.StorageKey));
        if (!fullPath.StartsWith(_storageRoot) || !File.Exists(fullPath))
            return ServiceResult<(string, string, string)>.NotFound($"The file for attachment {attachmentId} is missing.", "FILE_MISSING");

        return ServiceResult<(string, string, string)>.Ok((fullPath, attachment.MimeType, attachment.OriginalFileName));
    }

    public static AttachmentDto ToDto(ReportAttachment a) => new()
    {
        AttachmentId = a.AttachmentId,
        AttachmentType = a.AttachmentType,
        OriginalFileName = a.OriginalFileName,
        MimeType = a.MimeType,
        SizeBytes = a.SizeBytes,
        UploadedAt = a.UploadedAt
    };
}
