using System.ComponentModel.DataAnnotations;

namespace MineSafety.Api.Entities;

// Maps to the IncidentTypes table (database/schema/002_IncidentTypes.sql): Hazard, Incident and Near Miss.
public class IncidentType
{
    public int IncidentTypeId { get; set; }        // IncidentTypeID (primary key)

    [MaxLength(50)]
    public string TypeName { get; set; } = "";

    // When true, a report of this type sends an urgent alert straight away (true for Incident).
    public bool TriggersImmediateAlert { get; set; }

    public bool IsActive { get; set; } = true;
}
