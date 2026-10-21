# MineSafe: Android <-> API integration contract

**Purpose:** connect the Android app (`MineSafe-Mobile-FrontEnd`, package
`com.example.javareportingsystem`) to the real ASP.NET Core API (`MineSafe-Backend`).
The app was built against `tools/mock_api.py` (port 8080). The real API differs in a few
request/response shapes. **The API is the source of truth: change the Android app to match
this document.** Do not change screens, navigation, Room storage design or UI behaviour
beyond what is listed here.

Instructions for the coding agent:
1. Read this whole document first.
2. Apply the changes in section 4, file by file.
3. Keep `tools/mock_api.py` working only if cheap; otherwise mark it as legacy. The real API wins.
4. Do not add new libraries. Keep `HttpURLConnection` + `org.json` as used now.
5. minSdk is 25: do **not** use `java.time` (no desugaring). Use `SimpleDateFormat` (UTC).
6. After the changes, run through the acceptance checks in section 6.

### Change log
- **21 Oct:** Supervisor reports are approved automatically once their evidence is complete
  (sections 3.6, 3.7, 3.9). **No Android change is required**: the app already sends
  `expectedImageCount` / `expectedVoiceNoteCount` and a stable attachment ID. New optional
  response fields: `evidenceComplete`, `autoApproved`, `expectedImageCount`,
  `expectedVoiceNoteCount` on reports; `reportStatusCode`, `evidenceComplete` on uploads.
  New error `409 EVIDENCE_INCOMPLETE` when approving a report whose files are still uploading.

---

## 1. Base URL

| Where the app runs | `api_base_url` |
|---|---|
| Android emulator, API on same PC | `http://10.0.2.2:5080` |
| Real phone on same Wi-Fi as API laptop | `http://<laptop IPv4>:5080` (API started with `dotnet run --launch-profile lan`) |

Change `app/src/debug/res/values/api_config.xml` from port **8080** to **5080**.
Cleartext HTTP is already allowed in the debug network security config. Keep that.

## 2. Global conventions (apply everywhere)

- **Auth header:** every call except `/health` and `/api/auth/login` sends
  `Authorization: Bearer <token>`.
- **IDs are JSON numbers** (`"reportId": 12`, `"locationId": 2`, `"attachmentId": 7`).
  The app stores them as strings: read them with `optString(...)`/`String.valueOf(getInt(...))`.
- **Dates are ISO 8601 UTC text, never epoch numbers**, e.g. `"2026-10-19T08:30:00Z"`.
  - Sending: format epoch millis with `SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss'Z'", Locale.US)`, time zone UTC.
  - Reading: parse with the same pattern, but **first strip any fractional seconds**
    (`value.replaceAll("\\.\\d+Z$", "Z")`) so both `...:00Z` and `...:00.1234567Z` work.
  - Create one shared helper, e.g. `data/ApiDates.java` with `toIso(long millis)` and
    `parseIsoToMillis(String iso)` (returns 0 on null/empty/invalid), and use it everywhere
    the mock used `...EpochMillis` fields.
- **Lists** always look like `{ "items": [ ... ], "page": 1, "pageSize": 20, "total": 3 }`
  (exception: `GET /api/locations` and `GET /api/incident-types` return a **plain JSON array**).
- **Single report** responses are wrapped: `{ "report": { ... } }`.
- **Errors** always look like `{ "code": "ALREADY_REVIEWED", "message": "Human readable text" }`.
  Validation errors add `"errors": { "Description": ["..."] }`. 401 and 403 have an **empty body**.
  The app already reads `code`/`message` this way. Keep showing `message`.
- **Status codes:** 200 OK, 201 Created, 204 No Content, 400 bad input, 401 not logged in /
  token expired / account deactivated, 403 wrong role, 404 not found or not yours,
  409 conflict, 413 file over 25 MB.
- **Role names from the API:** `Worker`, `Supervisor`, `SafetyOfficer`, `Administrator`.
  The app compares against `WORKER`, `SUPERVISOR`, `SAFETY_OFFICER`. Map when parsing
  login (section 4.2) so the rest of the app keeps working unchanged.
- **Code values (same as the app already uses):** types `HAZARD` / `INCIDENT` / `NEAR_MISS`;
  statuses `PENDING_REVIEW` / `APPROVED` / `DENIED`. In report JSON these are in
  **`typeCode`** and **`statusCode`**. (`status` holds a display name like `"Pending Review"`,
  so do **not** use `status` for logic.)

## 3. Endpoints the app uses (exact shapes)

### 3.1 Health
`GET /health` (no auth) -> `200 { "status": "ok", "time": "..." }`. Already matches.

### 3.2 Login
`POST /api/auth/login`
```json
{ "login": "W1001", "password": "Password123!" }
```
`login` = worker ID **or** email. **The field is `login`, not `workerId`.**

`200`:
```json
{
  "token": "eyJhbGciOi...",
  "expiresAt": "2026-10-20T06:30:00Z",
  "user": {
    "accountId": 1,
    "workerId": "W1001",
    "fullName": "Test Worker",
    "email": "worker@mine.test",
    "roles": ["Worker"],
    "locations": [
      { "locationId": 2, "locationName": "Tunnel 7", "locationCode": "T-7", "isDefault": true },
      { "locationId": 3, "locationName": "Stope 12B", "locationCode": "ST-12B", "isDefault": false }
    ]
  }
}
```
`401 { "code": "INVALID_LOGIN", "message": "Invalid login details." }` covers wrong
password **and** inactive account (there is no separate 403 for inactive).

**There is no refresh token, no `/api/auth/refresh`, no `/api/auth/logout`.**
The token lasts 12 hours (one shift). When it expires, any call returns 401: keep queued
reports in Room and send the user to the login screen; after login the queue uploads.
Logout = clear the local session only.

### 3.3 Current user
`GET /api/auth/me` -> `200` with the same object as `user` above. `401` = session invalid.

### 3.4 Locations (for the location picker)
`GET /api/locations` -> `200` **plain array** (no paging):
```json
[
  { "locationId": 2, "locationName": "Tunnel 7", "locationCode": "T-7",
    "locationInfo": "Haulage tunnel, level 3", "isActive": true }
]
```
Workers and Supervisors only receive their **assigned** locations; Safety Officers and
Administrators receive all.

### 3.5 Incident types (optional; the app already hard-codes the 3 codes)
`GET /api/incident-types` -> `[{ "incidentTypeId": 2, "typeCode": "INCIDENT", "typeName": "Incident", "triggersImmediateAlert": true }, ...]`

### 3.6 Submit a report (sync)
`POST /api/reports` (roles: Worker, Supervisor)
```json
{
  "clientReportId": "6f1c2a4e-1b7d-3c55-9a0e-2f4b8d1e7c90",
  "typeCode": "INCIDENT",
  "locationId": 2,
  "description": "Damaged cable near the haulage track.",
  "occurredAt": "2026-10-19T08:30:00Z",
  "expectedImageCount": 2,
  "expectedVoiceNoteCount": 1
}
```
- `clientReportId` (**required**, UUID string): identifies this report forever. Must be the
  **same value on every retry** of the same report. See 4.4 for how to make it.
- `locationId` (**required**, number): the **server** location ID (`LocationEntity.serverLocationId`).
- `description`: text, max 4000. May be empty (`""`) when the report has a voice note.
- `expectedImageCount` / `expectedVoiceNoteCount`: how many photos / voice notes the app
  **will upload** for this report (0 if none). Send the real numbers: the server stores them and
  the report's evidence counts as complete only when that many files have arrived.
- **Supervisor reports** (the logged-in account has the Supervisor role, decided by the server):
  a supervisor can't review their own report, so it is **approved automatically** as soon as its
  evidence is complete: immediately if both counts are 0, otherwise when the last expected file
  uploads. The response / My Reports then show `statusCode: "APPROVED"` with
  `autoApproved: true`. Worker reports are unchanged (they wait for a supervisor).
- `occurredAt` (**required**): ISO UTC. Not in the future.
- Do **not** send: `deviceId`, `localReportId`, `locationCode`, `reporterWorkerId`,
  `...EpochMillis`, `anonymous`. The reporter is always the logged-in account (from the token).

Responses:
- `201` new report, or `200` **already received earlier** (safe retry). Both mean "synced":
```json
{
  "alreadyExisted": false,
  "report": {
    "reportId": 12,
    "reportNumber": "MS-2026-00012",
    "clientReportId": "6f1c2a4e-...",
    "typeCode": "INCIDENT", "incidentType": "Incident", "isUrgent": true,
    "locationId": 2, "locationCode": "T-7", "locationName": "Tunnel 7",
    "statusCode": "PENDING_REVIEW", "status": "Pending Review",
    "statusUpdatedAt": "2026-10-19T09:02:11Z", "reviewReason": null,
    "reporterName": "Test Worker",
    "occurredAt": "2026-10-19T08:30:00Z", "submittedAt": "2026-10-19T09:02:11Z",
    "imageCount": 0, "voiceNoteCount": 0,
    "expectedImageCount": 2, "expectedVoiceNoteCount": 1,
    "evidenceComplete": false, "autoApproved": false,
    "description": "...", "reviews": [], "attachments": [],
    "canEdit": true, "canWithdraw": true, "canReview": false
  }
}
```
  Store `report.reportId` as `serverReportId`, `report.reportNumber` as `referenceNumber`,
  `report.submittedAt` (parsed) as `serverReceivedAtEpochMillis`.
- `400` (`LOCATION_NOT_ALLOWED`, `UNKNOWN_TYPE`, `OCCURRED_IN_FUTURE`, `VALIDATION_FAILED`):
  do not retry; mark the report failed and show `message`.
- `409 CLIENT_ID_CONFLICT`: the same `clientReportId` was used with different content.
  Should never happen if 4.4 is followed; mark failed.
- `401`: keep queued, require login. `403`: role can't submit (Safety Officer/Admin).
- No response / timeout / 5xx: retry later with the **same** body.

### 3.7 Upload evidence (after the report is synced)
`POST /api/reports/{reportId}/attachments`, `multipart/form-data`, only by the reporter.

| Part | Value |
|---|---|
| `file` | the bytes, with the correct part `Content-Type` |
| `attachmentType` | `PHOTO` or `VOICE_NOTE` |
| `clientAttachmentId` (optional) | stable local ID, e.g. `image-12` / `voice-4` |
| `capturedAt` (optional) | ISO UTC |

Allowed `Content-Type`: photos `image/jpeg`, `image/png`, `image/webp`, `image/heic`;
voice `audio/mp4`, `audio/m4a`, `audio/x-m4a`, `audio/aac`, `audio/mpeg`, `audio/3gpp`,
`audio/amr`, `audio/ogg`, `audio/wav`, `audio/webm`. Max 25 MB.

Responses: `201` saved, or `200` **this exact file is already attached** (matched by SHA-256,
so re-uploading is always safe):
```json
{ "alreadyExisted": false,
  "attachment": { "attachmentId": 7, "attachmentType": "PHOTO", "originalFileName": "evidence",
                  "mimeType": "image/jpeg", "sizeBytes": 183422, "uploadedAt": "2026-10-19T09:02:15Z" } }
```
Read the ID from **`attachment.attachmentId`** (not top level). The response also has
`"reportStatusCode"` (e.g. `"APPROVED"` when this was a supervisor report's last expected file)
and `"evidenceComplete"`.

`clientAttachmentId` (or the older name `localAttachmentId`): send the **same** stable ID when
retrying an upload; the server returns the original attachment (`200`). Different IDs are
different files, even if the bytes are identical. Without an ID, an identical file on the same
report is matched by its SHA-256 fingerprint.
`400 UNSUPPORTED_FILE_TYPE` / `INVALID_ATTACHMENT_TYPE` / `EMPTY_FILE`, `413 FILE_TOO_LARGE`:
do not retry, ask the user to replace the file. Unknown multipart fields are ignored.

### 3.8 My Reports (status refresh)
`GET /api/reports/mine?page=1&pageSize=100` -> list of report summaries (same fields as
`report` above minus `description`, `reviews`, `attachments`, `can...`).
Use per item: `reportId`, `clientReportId`, `statusCode`, `statusUpdatedAt` (ISO),
`reviewReason` (denial reason or `null`).
Optional filter: `&status=PENDING_REVIEW`.

### 3.9 Supervisor
- `GET /api/supervisor/reports?status=PENDING_REVIEW&page=1&pageSize=100` -> list. Only
  reports at the supervisor's assigned locations, never their own. Pending = urgent first.
- `GET /api/supervisor/reports/{reportId}` -> `{ "report": { ... full detail ... } }`
- `POST /api/supervisor/reports/{reportId}/review` with `{ "decision": "APPROVE" }` or
  `{ "decision": "DENY", "reason": "..." }` -> `{ "report": { ... } }`.
  `400 REASON_REQUIRED`, `400 OWN_REPORT`, `409 ALREADY_REVIEWED`, `404`,
  `409 EVIDENCE_INCOMPLETE` (APPROVE is refused while expected photos / voice notes are still
  uploading; DENY is still allowed). Report items include `evidenceComplete`: show the reviewer
  "Evidence still uploading" and disable Approve while it is `false`.
  These routes and the request body **already match** the app.

### 3.10 Safety Officer (read-only)
- `GET /api/safety-officer/incidents?page=1&pageSize=100` -> list (Incidents that are pending
  or approved; denied ones are excluded).
- `GET /api/safety-officer/incidents/{reportId}` -> `{ "report": { ..., "dashboardUrl": "http://..." } }`
- Routes already match the app.

### 3.11 Download evidence
`GET /api/attachments/{attachmentId}` with the bearer token -> the file bytes with the right
`Content-Type`. `404` if missing or not visible. Already matches the app.

### 3.12 Notifications (optional for the app)
`GET /api/notifications?unreadOnly=true` -> list of `{ notificationId, notificationType,
message, reportId, reportNumber, createdAt, readAt }`. Types: `INCIDENT_ALERT`, `NEW_REPORT`,
`REPORT_REVIEWED`, `INCIDENT_APPROVED`. `POST /api/notifications/{id}/read` marks one read.

---

## 4. Required Android changes (file by file)

### 4.1 `app/src/debug/res/values/api_config.xml`
`http://10.0.2.2:8080` -> `http://10.0.2.2:5080`.

### 4.2 `data/ApiLoginDataSource.java`
- `login()`: request body `{ "login": workerId, "password": password }` (rename the key only).
- 401 -> "Worker ID or password is incorrect." (there is no 403-inactive case any more).
- `parseLoginResponse()`:
  - token: `response.getString("token")`
  - expiry: `ApiDates.parseIsoToMillis(response.getString("expiresAt"))`
  - user object: `response.getJSONObject("user")` (not `account`)
  - `accountId`, `workerId`, `fullName` from `user`
  - role: take `user.getJSONArray("roles")`, map each with
    `Worker->WORKER`, `Supervisor->SUPERVISOR`, `SafetyOfficer->SAFETY_OFFICER`,
    `Administrator->ADMINISTRATOR`. If several, pick by priority
    `SUPERVISOR` > `SAFETY_OFFICER` > `WORKER` > `ADMINISTRATOR`.
  - assigned locations: from `user.getJSONArray("locations")`, add each `locationCode`
    (string list, as the app stores today).
  - refresh token: there is none. Pass `""`.
- `refresh()`: do not call the network. Return `Result.Error(new IOException("Session expired. Sign in again."))`.
- `logout()`: do not call the network (local sign-out only).
- If `ADMINISTRATOR` is the only role, show "Administrators use the web dashboard." and do not log in.

### 4.3 `data/SessionStore.java` and `data/LoginRepository.java`
- Allow an **empty** refresh token: remove the checks that throw when
  `refreshToken.isEmpty()` (in `SessionStore.restore()` and in the login parse).
- `LoginRepository.getFreshUser()`: if the access token is expired, return `null`
  (the user must log in again). Do not attempt a refresh.

### 4.4 Stable `clientReportId` (no database migration needed)
Make the ID deterministic from things the app already stores, so every retry sends the same value:
```java
// DeviceConfigEntity.installationUuid is already a stable per-install UUID.
static String clientReportId(String installationUuid, long localReportId) {
    return UUID.nameUUIDFromBytes(
            ("minesafe-report:" + installationUuid + ":" + localReportId)
                    .getBytes(StandardCharsets.UTF_8)).toString();
}
```
Use `reports.getOrCreateDeviceConfig().installationUuid` + `report.localId`.

### 4.5 `data/ApiReportingDataSource.java`
- **Delete** `registerDevice(...)` (there is no `/api/devices`).
- `submitReport(JSONObject)`: unchanged transport (POST `/api/reports`). The caller builds the
  new body (4.7). Treat `200` and `201` the same.
- `getMyReports(...)`: no `deviceId` parameter. Loop pages of
  `GET /api/reports/mine?page=N&pageSize=100` until `items` is empty or all `total` collected.
- `uploadAttachment(...)`: keep the multipart code. Change the form fields to:
  `attachmentType` (PHOTO / VOICE_NOTE), `clientAttachmentId` (the existing local ID string),
  `capturedAt` (`ApiDates.toIso(capturedAt)`). Remove `captureSource` and
  `capturedAtEpochMillis` (they're ignored anyway). Keep the part `Content-Type` = real MIME type.
  Give the file part a filename with an extension (`evidence.jpg`, `evidence.m4a`, ...).
- The response parser already reads `message` for errors; also expose `code` and the HTTP
  status (like `ApiReportReviewDataSource.ApiException`) so the worker can tell
  "retry later" from "fix and resubmit".

### 4.6 `data/ApiLocationDataSource.java`
- `GET /api/locations` (no paging params). Parse a **JSONArray** directly.
- For each item: `new RemoteLocation(String.valueOf(item.getInt("locationId")),
  item.getString("locationCode"), item.getString("locationName"))`.

### 4.7 `data/ReportSyncWorker.java`
- Remove all device registration (`registerDevice`, `setServerDeviceId`, `deviceId` params).
- Build the submit body:
```java
JSONObject body = new JSONObject()
    .put("clientReportId", clientReportId(device.installationUuid, report.localId))
    .put("typeCode", report.typeCode)
    .put("locationId", Integer.parseInt(location.serverLocationId))   // must be non-null
    .put("description", report.description == null ? "" : report.description)
    .put("occurredAt", ApiDates.toIso(report.occurredAtEpochMillis != null
            ? report.occurredAtEpochMillis : report.createdAtEpochMillis))
    .put("expectedImageCount", photos.size())
    .put("expectedVoiceNoteCount", voiceCount);
```
  If `location.serverLocationId == null`, throw `IOException("Location not synced yet")`
  so it retries after the location list refreshes.
- Read the response: `JSONObject r = accepted.getJSONObject("report");`
  `reportId = String.valueOf(r.getInt("reportId"));`
  `referenceNumber = r.optString("reportNumber", reportId);`
  `receivedAt = ApiDates.parseIsoToMillis(r.optString("submittedAt"))` (fallback: now).
- Attachments: `accepted.getJSONObject("attachment").getInt("attachmentId")` -> store as string.
  `200` (already existed) counts as success.
- `refreshStatuses`: for each item use
  `reportId = String.valueOf(item.getInt("reportId"))`,
  `status = item.getString("statusCode")`,
  `statusUpdatedAt = ApiDates.parseIsoToMillis(item.optString("statusUpdatedAt"))`,
  `reviewReason = item.isNull("reviewReason") ? null : item.getString("reviewReason")`.
- On HTTP 401 anywhere: stop, return `Result.success()` and leave items queued (sign-in will
  enqueue again). On 400/409/413 for one report: mark that report/attachment failed with
  `message`, continue with the others. On network errors / 5xx: `Result.retry()`.
- Skip sync entirely for `SAFETY_OFFICER` and `ADMINISTRATOR` (already done for SO).

### 4.8 `data/ApiReportReviewDataSource.java` (`parseReport`)
Routes and review body already match. Only change field names:

| Mock field | Real API field |
|---|---|
| `reportId` (string) | `reportId` (number: `String.valueOf(json.getInt("reportId"))`) |
| `status` | **`statusCode`** |
| `referenceNumber` | `reportNumber` |
| `type` | `typeCode` |
| `submittedAtEpochMillis` | `ApiDates.parseIsoToMillis(json.optString("submittedAt"))` |
| `occurredAtEpochMillis` | `ApiDates.parseIsoToMillis(json.optString("occurredAt"))` |
| `anonymous` | not sent; default `false` |
| `evidenceComplete` | not sent; default `true` (keep current default) |
| `attachments[].attachmentId` | number: `String.valueOf(item.getInt("attachmentId"))` |
| `imageCount`, `voiceNoteCount`, `reviewReason`, `locationCode`, `locationName`, `description`, `dashboardUrl` | same names |

### 4.9 Anonymous reporting
The API and database have **no anonymous flag** (every report needs a reporter account). For
this version: **hide/disable the anonymous option** in the UI, or keep the choice local only
with a note "Your name is shown to your supervisor". Do not send `anonymous`.

### 4.10 Clean-up (D4 "code quality" marks)
- Remove `ExampleUnitTest` / `ExampleInstrumentedTest` placeholders.
- Write a real `README.md` (what the app does, how to run, base URL, test accounts).
- Mark `tools/mock_api.py` as legacy/test-only in the README.

---

## 5. Test accounts (API in Development mode), password `Password123!`

| Worker ID | Role | Locations |
|---|---|---|
| W1001 | Worker | Tunnel 7 (default), Stope 12B |
| S2001 | Supervisor | Tunnel 7 (default), Stope 12B |
| S2002 | Supervisor | Shaft 1 Station (default), Workshop |
| O3001 | SafetyOfficer | all |
| A9001 | Administrator | (web only) |

## 6. Acceptance checks (do all of them on the emulator against the real API)

1. App start with API running: `/health` reachable, login screen shows.
2. Log in as `W1001`: lands on worker home; location picker shows **Tunnel 7 and Stope 12B only**.
3. Airplane mode on: create an Incident at Tunnel 7 with 1 photo + 1 voice note. It shows
   "Pending upload".
4. Airplane mode off: it syncs, gets `MS-2026-000xx`, shows "Pending review".
   Kill the app mid-sync and reopen: **still only one report** on the server.
5. Log in as `S2002`: the report is **not** in the review queue (different locations).
6. Log in as `S2001`: report is in the queue (urgent first), photo and voice note open.
   Deny without reason -> error shown. Deny with reason -> done. Second decision -> "already reviewed".
7. Log in as `W1001`: My Reports shows **Denied** with the reason.
8. Log in as `O3001`: Safety Officer incident list opens; denied incidents are not listed;
   an approved incident is listed and opens with its evidence. No sync runs for this role.
9. Wrong password -> "Worker ID or password is incorrect."
10. Log in as `A9001` -> "Administrators use the web dashboard."

## 7. Not in this version (agreed future work)
Refresh tokens; evidence-completeness blocking; device registration; anonymous flag;
push notifications (in-app list only).
