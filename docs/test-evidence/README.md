# API test evidence (18 October 2026)

Tested in Swagger against a local SQL Server database built from `database/schema`.

| # | Test | Result |
|---|---|---|
| 1 | Admin login (`A9001`) | 200, signed JWT with role Administrator |
| 2 | Worker (`W1001`) submits an Incident at Tunnel 7 | 201, `MS-2026-00001`, status Pending Review, reporter taken from token |
| 3 | Same `clientReportId` sent again (offline retry) | 200, `alreadyExisted: true`, same ReportID: no duplicate |
| 4 | Supervisor at other locations (`S2002`) opens report 1 | 404: location scoping, no data leakage |
| 5 | Supervisor at Tunnel 7 (`S2001`) checks alerts | `INCIDENT_ALERT` for MS-2026-00001 |
| 6 | `S2001` approves; then a second review attempt | 200 Approved; second attempt 409 (already reviewed) |
| 7 | Worker checks alerts | `REPORT_REVIEWED`: "Your report MS-2026-00001 was approved." |
| - | Request without token | 401 |
