using Microsoft.EntityFrameworkCore;
using MineSafety.Api.Common;
using MineSafety.Api.Data;
using MineSafety.Api.Dtos;

namespace MineSafety.Api.Services;

public interface ILookupService
{
    Task<List<IncidentTypeDto>> GetIncidentTypesAsync();
    Task<List<ReportStatusDto>> GetReportStatusesAsync();
}

// Small read-only lists the app needs for its buttons and filters.
public class LookupService : ILookupService
{
    private readonly MineSafetyDbContext _db;

    public LookupService(MineSafetyDbContext db)
    {
        _db = db;
    }

    public async Task<List<IncidentTypeDto>> GetIncidentTypesAsync()
    {
        var types = await _db.IncidentTypes.AsNoTracking().Where(t => t.IsActive)
                                           .OrderBy(t => t.IncidentTypeId).ToListAsync();
        return types.Select(t => new IncidentTypeDto
        {
            IncidentTypeId = t.IncidentTypeId,
            TypeCode = Codes.From(t.TypeName),
            TypeName = t.TypeName,
            TriggersImmediateAlert = t.TriggersImmediateAlert
        }).ToList();
    }

    public async Task<List<ReportStatusDto>> GetReportStatusesAsync()
    {
        var statuses = await _db.ReportStatuses.AsNoTracking().OrderBy(s => s.DisplayOrder).ToListAsync();
        return statuses.Select(s => new ReportStatusDto
        {
            StatusId = s.StatusId,
            StatusCode = Codes.From(s.StatusName),
            StatusName = s.StatusName,
            IsTerminal = s.IsTerminal
        }).ToList();
    }
}
