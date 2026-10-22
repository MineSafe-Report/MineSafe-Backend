using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Matches database/schema/IncidentTypes.sql : Hazard, Incident, Near Miss.
public class IncidentType
{
    public int IncidentTypeId { get; set; }          // IncidentTypeID (PK)

    [MaxLength(50)]
    public string TypeName { get; set; } = "";

    // If true, a report of this type creates an urgent alert straight away (Incident = true).
    public bool TriggersImmediateAlert { get; set; }

    public bool IsActive { get; set; } = true;
}
