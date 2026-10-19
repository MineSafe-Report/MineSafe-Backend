# Connecting the Android app to the MineSafe API

For Banjo (frontend). Everything the app needs to talk to the API, with Java examples.
Full endpoint list: `README.md` section 5. Live, clickable list: Swagger (`/swagger`).

---

## 1. Where the API is

| Running the app on... | Base URL in the app |
|---|---|
| Android **emulator** on the same PC as the API | `http://10.0.2.2:5080/` |
| A **real phone** on the same Wi-Fi as the API laptop | `http://<laptop IP>:5080/` e.g. `http://192.168.1.15:5080/` |

For the real phone, the API laptop must start the API with the network profile:
```
dotnet run --launch-profile lan
```
and allow port 5080 when Windows Firewall asks. Find the laptop IP with `ipconfig` (IPv4 Address).
Quick check: open `http://<laptop IP>:5080/swagger` in the phone's browser. If that loads, the app can connect.

## 2. Android setup

`AndroidManifest.xml`:
```xml
<uses-permission android:name="android.permission.INTERNET" />
<!-- usesCleartextTraffic: development only, because the API runs on plain http -->
<application
    android:usesCleartextTraffic="true"
    ... >
```

`app/build.gradle` dependencies:
```gradle
implementation 'com.squareup.retrofit2:retrofit:2.11.0'
implementation 'com.squareup.retrofit2:converter-gson:2.11.0'
implementation 'com.squareup.okhttp3:logging-interceptor:4.12.0'
implementation 'androidx.work:work-runtime:2.9.1'          // background sync
```

## 3. Login and the token

1. `POST /api/auth/login` with `{ "login": "W1001", "password": "Password123!" }`
   (login = worker ID or email).
2. Response `200`: save `token` and `expiresAt` (e.g. in SharedPreferences), plus `user.roles`
   and `user.locations`.
3. Every other request sends the header `Authorization: Bearer <token>`.
   The interceptor below does this automatically.
4. `401` on any request = token expired or account deactivated: clear the token, go to login.
5. **Logout** = delete the saved token. There is no logout endpoint.
6. On app start: if a token is saved, call `GET /api/auth/me`. `200` = still logged in, skip the login screen.

Use `user.roles` to decide which buttons to show (e.g. "Review Reports" only for `Supervisor`).
Use `user.locations` (with `isDefault`) to pre-select the location, or call `GET /api/locations`.

```java
public class AuthInterceptor implements Interceptor {
    private final SharedPreferences prefs;
    public AuthInterceptor(SharedPreferences prefs) { this.prefs = prefs; }

    @Override public Response intercept(Chain chain) throws IOException {
        Request request = chain.request();
        String token = prefs.getString("token", null);
        if (token != null) {
            request = request.newBuilder()
                    .header("Authorization", "Bearer " + token)
                    .build();
        }
        return chain.proceed(request);
    }
}

public class ApiClient {
    public static final String BASE_URL = "http://10.0.2.2:5080/";   // see section 1

    public static MineSafeApi create(Context context) {
        SharedPreferences prefs = context.getSharedPreferences("auth", Context.MODE_PRIVATE);
        HttpLoggingInterceptor log = new HttpLoggingInterceptor();
        log.setLevel(HttpLoggingInterceptor.Level.BODY);            // shows requests in Logcat

        OkHttpClient client = new OkHttpClient.Builder()
                .addInterceptor(new AuthInterceptor(prefs))
                .addInterceptor(log)
                .build();

        return new Retrofit.Builder()
                .baseUrl(BASE_URL)
                .client(client)
                .addConverterFactory(GsonConverterFactory.create())
                .build()
                .create(MineSafeApi.class);
    }
}
```

## 4. The Retrofit interface (the endpoints the app uses)

```java
public interface MineSafeApi {
    // Connection check (no login needed)
    @GET("health")                 Call<Void> health();

    // Auth
    @POST("api/auth/login")        Call<LoginResponse> login(@Body LoginRequest body);
    @GET("api/auth/me")            Call<CurrentUser> me();

    // Dropdowns
    @GET("api/incident-types")     Call<List<IncidentType>> incidentTypes();
    @GET("api/locations")          Call<List<Location>> locations();

    // Worker
    @POST("api/reports")           Call<SubmitReportResponse> submitReport(@Body CreateReportRequest body);
    @GET("api/reports/mine")       Call<PagedResult<ReportSummary>> myReports(@Query("page") int page, @Query("pageSize") int pageSize);
    @GET("api/reports/{id}")       Call<ReportEnvelope> report(@Path("id") int id);
    @PUT("api/reports/{id}")       Call<ReportEnvelope> updateReport(@Path("id") int id, @Body UpdateReportRequest body);
    @DELETE("api/reports/{id}")    Call<Void> withdrawReport(@Path("id") int id);

    @Multipart
    @POST("api/reports/{id}/attachments")
    Call<UploadAttachmentResponse> uploadAttachment(@Path("id") int reportId,
                                                    @Part MultipartBody.Part file,
                                                    @Part("attachmentType") RequestBody attachmentType,
                                                    @Part("clientAttachmentId") RequestBody clientAttachmentId);

    // Supervisor
    @GET("api/supervisor/reports")  Call<PagedResult<ReportSummary>> supervisorReports(@Query("status") String status,
                                                                                      @Query("page") int page, @Query("pageSize") int pageSize);
    @GET("api/supervisor/reports/{id}") Call<ReportEnvelope> supervisorReport(@Path("id") int id);
    @POST("api/supervisor/reports/{id}/review") Call<ReportEnvelope> review(@Path("id") int id, @Body ReviewRequest body);

    // Safety Officer (read-only)
    @GET("api/safety-officer/incidents")      Call<PagedResult<ReportSummary>> incidents(@Query("status") String status);
    @GET("api/safety-officer/incidents/{id}") Call<ReportEnvelope> incident(@Path("id") int id);

    // Alerts
    @GET("api/notifications")      Call<PagedResult<NotificationItem>> notifications(@Query("unreadOnly") boolean unreadOnly);
    @POST("api/notifications/{id}/read") Call<NotificationItem> markRead(@Path("id") int id);
}
```

Uploading a photo:
```java
RequestBody fileBody = RequestBody.create(photoFile, MediaType.parse("image/jpeg"));
MultipartBody.Part filePart = MultipartBody.Part.createFormData("file", photoFile.getName(), fileBody);
RequestBody type = RequestBody.create("PHOTO", MediaType.parse("text/plain"));
RequestBody clientId = RequestBody.create(localAttachmentUuid, MediaType.parse("text/plain"));
api.uploadAttachment(reportId, filePart, type, clientId).enqueue(...);
```
`201` = saved, `200` = the same file was already there (safe retry), `413` = over 25 MB.
Voice notes: `attachmentType` `VOICE_NOTE`, MIME e.g. `audio/mp4`.

## 5. The JSON shapes (Java models)

Field names must match the JSON exactly (camelCase). Only the fields the app needs are shown.

```java
class LoginRequest      { String login; String password; }
class LoginResponse     { String token; String expiresAt; CurrentUser user; }
class CurrentUser       { int accountId; String workerId; String fullName; String email;
                          List<String> roles; List<AssignedLocation> locations; }
                          // roles: "Worker", "Supervisor", "SafetyOfficer", "Administrator"
class AssignedLocation  { int locationId; String locationName; String locationCode; boolean isDefault; }
class Location          { int locationId; String locationName; String locationCode; String locationInfo; boolean isActive; }
class IncidentType      { int incidentTypeId; String typeCode; String typeName; boolean triggersImmediateAlert; }

class CreateReportRequest { String clientReportId; String typeCode; int locationId;
                            String description; String occurredAt;
                            Integer expectedImageCount; Integer expectedVoiceNoteCount; }
class UpdateReportRequest { String typeCode; int locationId; String description; String occurredAt; }
class SubmitReportResponse { boolean alreadyExisted; ReportDetail report; }
class ReportEnvelope    { ReportDetail report; }

class ReportSummary { int reportId; String reportNumber; String clientReportId;
                      String typeCode; String incidentType; boolean isUrgent;
                      int locationId; String locationCode; String locationName;
                      String statusCode; String status; String statusUpdatedAt; String reviewReason;
                      String reporterName; String occurredAt; String submittedAt;
                      int imageCount; int voiceNoteCount; }
class ReportDetail extends ReportSummary {
                      String description; List<Review> reviews; List<Attachment> attachments;
                      String dashboardUrl;  // Safety Officer only
                      boolean canEdit; boolean canWithdraw; boolean canReview; }
class Review        { String decision; String comment; String reviewerName; String reviewedAt; }
class Attachment    { int attachmentId; String attachmentType; String originalFileName;
                      String mimeType; long sizeBytes; String uploadedAt; }
class UploadAttachmentResponse { boolean alreadyExisted; Attachment attachment; }
class ReviewRequest { String decision; String reason; }   // "APPROVE" or "DENY" (reason required for DENY)

class NotificationItem { int notificationId; String notificationType; String message;
                         int reportId; String reportNumber; String createdAt; String readAt; }
class PagedResult<T> { List<T> items; int page; int pageSize; int total; }
class ApiError       { String code; String message; Map<String, List<String>> errors; }
```

Codes: `typeCode` = `INCIDENT` / `HAZARD` / `NEAR_MISS`; `statusCode` = `PENDING_REVIEW` /
`APPROVED` / `DENIED` ("Pending upload" is the phone's own state before sync).
Use `canEdit` / `canWithdraw` / `canReview` to show or hide buttons.
To show a photo: `GET api/attachments/{attachmentId}` (with the token, e.g. via Glide/OkHttp).

## 6. Dates

All dates are UTC in ISO 8601 with a `Z`, e.g. `"2026-10-18T09:30:00Z"`.
- Sending: `Instant.now().toString()` gives exactly this format (API 26+).
  Record `occurredAt` when the worker starts the report, not when it uploads.
- Showing: parse with `Instant.parse(...)` and convert to the phone's time zone for display.

## 7. Offline reporting and sync (the important bit)

1. When the worker starts a report, create its ID **on the phone** and store it in Room:
   `String clientReportId = UUID.randomUUID().toString();`
2. Save the whole report locally first (Room), status "Pending upload".
3. A WorkManager job (with a "network connected" constraint) sends `POST /api/reports`
   for every pending report, **always with the same `clientReportId`**.
4. Handle the answer:

| Response | Meaning | What the app does |
|---|---|---|
| `201` | New report saved | Store `report.reportId` + `reportNumber`, mark synced |
| `200` + `alreadyExisted: true` | Server already had it (earlier attempt got through) | Same as 201: mark synced |
| `400` | Bad data (`code` e.g. `LOCATION_NOT_ALLOWED`) | Mark failed, show `message` to the worker |
| `401` | Logged out / token expired | Keep it queued, ask the worker to log in |
| `409` `CLIENT_ID_CONFLICT` | That `clientReportId` was used for a different report | Bug on the phone side: the ID must belong to one report only |
| No response / timeout | Signal dropped | Do nothing, WorkManager retries later with the same ID |

Because of the duplicate protection, retrying is always safe: the worker can never create two copies.

Photos and voice notes: upload **after** the report is synced, using the `reportId` from the
response (`POST /api/reports/{id}/attachments`, section 4). Retrying an upload is safe too.

## 8. Errors

Every error body: `{ "code": "ALREADY_REVIEWED", "message": "..." }`. Check `code` in code,
show `message` to the user. Validation errors use `code` `VALIDATION_FAILED` plus
`"errors": { "Description": ["..."] }`.
- `401` = not logged in / token expired (empty body) | `403` = wrong role (e.g. a Worker calling review)
- `404` = not found, or not yours to see | `409` = conflict (`ALREADY_REVIEWED`, `CLIENT_ID_CONFLICT`)
- `413` = file larger than 25 MB

## 9. Test accounts (password `Password123!`)

`W1001` Worker (Tunnel 7, Stope 12B) | `S2001` Supervisor (Tunnel 7, Stope 12B) |
`S2002` Supervisor (Shaft 1 Station, Workshop) | `O3001` Safety Officer | `A9001` Admin
