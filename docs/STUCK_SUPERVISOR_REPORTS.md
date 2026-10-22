# One-time fix: Supervisor reports stuck in Pending Review

**Why they're stuck:** before 21 October every report started as Pending Review, and a supervisor
can't review their own report. Reports submitted by S2001 / S2002 at a location with no other
supervisor could never be reviewed.

**Why they can't simply all be approved:** reports created before the AddEvidenceTracking migration have no stored
`ExpectedImageCount` / `ExpectedVoiceNoteCount`, so the server can't tell whether some of their
photos or voice notes are still waiting on a phone. Approving those blindly could approve a report
whose evidence never arrived.

## Step 1: update the database
```powershell
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -d MineSafetyDb -b -I -i database/migrations/AddEvidenceTracking.sql
```

## Step 2: look first (read-only)
Either run this SQL:
```sql
SELECT r.ReportID, r.ReportNumber, a.WorkerID, l.LocationName, r.SubmittedAt,
       r.ExpectedImageCount, r.ExpectedVoiceNoteCount,
       SUM(CASE WHEN ra.AttachmentType = 'PHOTO'      AND ra.IsDeleted = 0 THEN 1 ELSE 0 END) AS Photos,
       SUM(CASE WHEN ra.AttachmentType = 'VOICE_NOTE' AND ra.IsDeleted = 0 THEN 1 ELSE 0 END) AS VoiceNotes
FROM Reports r
JOIN ReportStatuses s ON s.StatusID = r.StatusID AND s.StatusName = 'Pending Review'
JOIN Accounts a ON a.AccountID = r.ReporterAccountID
JOIN Locations l ON l.LocationID = r.LocationID
LEFT JOIN ReportAttachments ra ON ra.ReportID = r.ReportID
WHERE r.IsActive = 1
  AND EXISTS (SELECT 1 FROM AccountRoles ar JOIN Roles ro ON ro.RoleID = ar.RoleID
              WHERE ar.AccountID = r.ReporterAccountID AND ar.IsActive = 1 AND ro.RoleName = 'Supervisor')
GROUP BY r.ReportID, r.ReportNumber, a.WorkerID, l.LocationName, r.SubmittedAt,
         r.ExpectedImageCount, r.ExpectedVoiceNoteCount
ORDER BY r.SubmittedAt;
```
or, in Swagger as **A9001**: `POST /api/admin/maintenance/stuck-supervisor-reports` with
`{ "dryRun": true }`. Each report comes back with an action:

| Action | Meaning |
|---|---|
| `WOULD_APPROVE` | Expected counts known and all evidence arrived. Safe. |
| `WAITING_FOR_EVIDENCE` | Files still missing. Leave it; it approves itself when they upload. |
| `NEEDS_CONFIRMATION` | Older report, counts unknown. Shows photos / voice notes received so far. |

## Step 3: check the NEEDS_CONFIRMATION ones
For each, ask the reporting supervisor to open **My Reports** on their phone: if nothing is
"Pending upload" for that report, its evidence is all on the server.

## Step 4: apply
```json
{ "dryRun": false, "confirmLegacyReportIds": [4, 7] }
```
- Complete reports are approved and audited as `AUTO_APPROVED`.
- Listed older reports are approved and audited as `AUTO_APPROVED_BY_ADMIN_CHECK`, with the
  **administrator** as the actor (honest record of who decided).
- Approved Incidents notify the Safety Officers once. Running it again changes nothing that is
  already approved.
