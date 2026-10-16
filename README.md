# MineSafe API

The backend for the mine safety reporting app (ITMDA3 Deliverable 4).
ASP.NET Core Web API (.NET 9) + EF Core + SQL Server + JWT login.

**Built so far:** login (multi-role, re-checked every request), Locations (full CRUD, location-scoped), Accounts (full CRUD with AccountRoles and AccountLocations), Roles list.
**Next:** Reports (submit, list, view, photo upload, supervisor review), Notifications.

---

## 1. How to run it

**One-time setup: create the database from Michael's scripts**

The API does **not** create tables. The SQL scripts in `database/schema` are the source of truth.

```powershell
./scripts/create-local-db.ps1
```

This creates a `MineSafetyDb` database in SQL Server LocalDB (installed with Visual Studio) and
runs the 12 schema scripts in order. No `sqlcmd`? See the comment at the top of the script for
the SSMS route.

**Then run the API**

1. Open `MineSafetyApi.csproj` in Visual Studio and press **F5** (or `dotnet run`).
2. On first run in Development it fills the empty tables with test data (`Data/DbSeeder.cs`).
3. Your browser opens on **http://localhost:5080/swagger**.

### Test accounts (password for all: `Password123!`)

| Worker ID | Role | Locations |
|---|---|---|
| W1001 | Worker | Tunnel 7 (default), Stope 12B |
| S2001 | Supervisor | Tunnel 7 (default), Stope 12B |
| S2002 | Supervisor | Shaft 1 Station (default), Workshop |
| O3001 | SafetyOfficer | all (role sees everything) |
| A9001 | Administrator | all (role sees everything) |

### Testing in Swagger

1. Open `POST /api/auth/login` > **Try it out** > send `{ "login": "A9001", "password": "Password123!" }`
2. Copy the `token` value from the response (without the quotes).
3. Click the **Authorize** button at the top, paste the token, click Authorize.
4. Now every other endpoint works as that user. Try `GET /api/locations`.
5. Permissions: log in as `W1001` and try `POST /api/locations`: you get **403**.
6. Location scoping: log in as `S2002` and call `GET /api/locations`: you only get Shaft 1 Station
   and Workshop. `GET /api/locations/2` (Tunnel 7) returns **404**.

---

## 2. The folders (what lives where)

| Folder | What it is | Analogy |
|---|---|---|
| `Program.cs` | Start-up: registers services, sets the order requests flow through | Opening the shop in the morning |
| `Controllers/` | Receive HTTP requests, call a service, return a status code | The receptionist |
| `Services/` | The actual rules and work (checks, saving, hashing) | The person doing the job |
| `Dtos/` | Shapes of the JSON going in and out | The forms people fill in |
| `Entities/` | C# classes that mirror the database tables (from Michael's SQL scripts) | The filing cabinet layout |
| `Data/` | `MineSafetyDbContext` (EF Core's link to the DB) and the test-data seeder | The filing clerk |
| `Common/` | Small shared helpers (role names, ServiceResult) | |
| `scripts/` | `create-local-db.ps1`: builds the dev database from `database/schema` | |
| `database/` | **Michael's** SQL schema, migrations and seed scripts. Don't edit without him. | |

---

## 3. Follow one request all the way through

**The app calls `POST /api/locations` with a token and `{ "locationName": "Tunnel 9", "locationCode": "t-9" }`**

1. **Authentication** (`Program.cs`, `UseAuthentication`): reads the token, checks the signature
   and expiry, and works out who the user is and what role they have. No/bad token = **401**.
2. **Authorization** (`UseAuthorization`): the method has `[Authorize(Roles = Administrator)]`.
   Not an admin = **403**.
3. **Routing** (`MapControllers`): `POST` + `/api/locations` matches `LocationsController.Create`.
4. **Validation** (`[ApiController]` + the attributes in `CreateLocationRequest`): missing name
   or a code longer than 20 characters = **400** with the field errors. Our code never runs.
5. **Controller** (`LocationsController.Create`): passes the request to `ILocationService`.
6. **Service** (`LocationService.CreateAsync`): uppercases the code to `T-9`, checks no other
   location uses it (else **409 Conflict**), builds a `Location` entity, `Add`, `SaveChangesAsync`.
7. **EF Core** writes the `INSERT` and SQL Server gives the new row its `LocationId`.
8. **Back in the controller:** the service returned success, so it sends **201 Created** with the
   new location as JSON.

Every endpoint follows this same path. Once you understand this one, you understand all of them.

---

## 4. Key ideas used in the code

- **Dependency injection:** `Program.cs` says "when someone asks for `ILocationService`, give them a
  `LocationService`". Controllers just list what they need in their constructor and receive it.
- **Interfaces** (`ILocationService`): the controller only knows *what* the service can do, not *how*.
- **async / await:** database calls take time. `await` lets the server serve other requests while
  it waits, instead of freezing.
- **ServiceResult:** services return "it worked + the value" or "it failed + why". Controllers
  translate "why" into 404 / 409 / 400 in one place (`ApiControllerBase.ErrorResponse`).
- **Soft delete:** `DELETE` sets `IsActive = false` instead of removing the row, because D3 says old
  reports must keep their account and location. Inactive accounts can't log in; inactive locations
  disappear from the worker's dropdown.
- **Password hashing:** `PasswordHasher` turns a password into a one-way hash. We store the hash,
  and at login we hash what was typed and compare.
- **JWT:** after login the app gets a signed token holding the account ID and roles. It sends it
  with every request as the header `Authorization: Bearer <token>`. Lasts 12 hours (one shift).
  "Logout" = the app deletes its saved token.
- **Roles via AccountRoles:** `Accounts` has no role column. Each row in `AccountRoles` links one
  account to one role, so an account can have several. Removing a role sets `IsActive = false` on
  that row (history is kept, and SQL only allows one row per account + role).
- **Roles re-checked on every request** (`Services/RoleRefresher.cs`): after the token is verified,
  the API loads the account's *current* active roles from the database. A deactivated account or a
  removed role stops working on the very next request, even if the token hasn't expired.
- **Location scoping** (`Services/AccessService.cs`): Workers and Supervisors only see the locations
  assigned to them in `AccountLocations`. Safety Officers and Admins see all. Anything outside your
  locations answers 404, so you can't even tell it exists.

---

## 5. Endpoints (for Banjo)

Errors always come back as `{ "message": "..." }`, except validation errors (400), which list the
fields that failed under `errors`.

### Auth
| Method | URL | Who | Body / query | Returns |
|---|---|---|---|---|
| POST | `/api/auth/login` | anyone | `{ login, password }` | 200 `{ token, expiresAt, user{accountId, workerId, fullName, role} }` or 401 |
| GET | `/api/auth/me` | logged in | | 200 user, or 401 if token expired |

### Locations
| Method | URL | Who | Body / query | Returns |
|---|---|---|---|---|
| GET | `/api/locations` | logged in | `?search=&includeInactive=` (inactive admin only) | 200 list |
| GET | `/api/locations/{id}` | logged in | | 200 / 404 |
| POST | `/api/locations` | admin | `{ locationName, locationCode, locationInfo }` | 201 / 400 / 409 |
| PUT | `/api/locations/{id}` | admin | `{ locationName, locationCode, locationInfo, isActive }` | 200 / 404 / 409 |
| DELETE | `/api/locations/{id}` | admin | | 204 / 404 (soft delete) |

### Accounts
| Method | URL | Who | Body / query | Returns |
|---|---|---|---|---|
| GET | `/api/accounts` | admin | `?search=&roleId=&includeInactive=` | 200 list |
| GET | `/api/accounts/{id}` | admin | | 200 / 404 |
| POST | `/api/accounts` | admin | `{ workerId, fullName, email, password, roleIds: [..], locationIds: [..], defaultLocationId }` | 201 / 400 / 409 |
| PUT | `/api/accounts/{id}` | admin | `{ fullName, email, roleIds, locationIds, defaultLocationId, isActive, newPassword? }` (full new state) | 200 / 400 / 404 / 409 |
| DELETE | `/api/accounts/{id}` | admin | | 204 / 400 / 404 (soft delete) |
| GET | `/api/roles` | admin | | 200 list (for the role dropdown) |

Account responses include `roles: [{ roleId, roleName }]` and
`locations: [{ locationId, locationName, locationCode, isDefault }]`. Login and `/api/auth/me` return
`roles` as a list of names, plus the same `locations` list (the app can pre-select the default).

Rules: email is required; Workers and Supervisors need at least one location; an admin can't
deactivate themselves or remove their own Administrator role.

---|---|---|---|---|
| GET | `/api/accounts` | admin | `?search=&roleId=&includeInactive=` | 200 list |
| GET | `/api/accounts/{id}` | admin | | 200 / 404 |
| POST | `/api/accounts` | admin | `{ workerId, fullName, email, password, roleId }` | 201 / 400 / 409 |
| PUT | `/api/accounts/{id}` | admin | `{ fullName, email, roleId, isActive, newPassword? }` | 200 / 400 / 404 / 409 |
| DELETE | `/api/accounts/{id}` | admin | | 204 / 400 / 404 (soft delete) |
| GET | `/api/roles` | admin | | 200 list (for the role dropdown) |

---

## 6. Alignment with the database (Michael's schema)

The C# entities follow `database/schema/*.sql` exactly. Where a SQL column name differs from the
C# property, it is mapped explicitly in `Data/MineSafetyDbContext.cs`:

| C# property | SQL column |
|---|---|
| `Account.WorkerId` | `Accounts.WorkerID` |
| `AccountLocation.AccountLocationId` | `AccountLocations.AccountLocation` (PK) |
| `ReportAttachment.UploadedByAccountId` | `ReportAttachments.UploadedByAccount` |
| `ReportAuditLog.OldValueJson / NewValueJson` | `OldValueJSON / NewValueJSON` |

- The API never creates or alters tables (no `EnsureCreated`, no migrations).
- Test data is seeded by the API in Development only, table by table, and only into empty tables.
  Test accounts are seeded in C# because the password hashes must come from the same
  `PasswordHasher` the login uses. Reference data (roles, types, statuses, locations) can move to
  Michael's `database/seed` scripts at any time: the seeder skips tables that already have rows.
- The JWT key is not in `appsettings.json`. Development uses `appsettings.Development.json`
  (dev-only key); anywhere else set the environment variable `Jwt__Key`.

### Known gaps (planned next)
- Reports: submit (with `ClientReportId` duplicate protection), list, view, edit.
- Attachments, reviews, audit log, notifications.
- Not yet run against a live SQL Server built from the scripts: needs testing on a dev machine.

## 7. Connecting the phone later

`localhost` only works on the laptop itself. For Banjo's phone or emulator:
- Android **emulator**: use `http://10.0.2.2:5080` from the app.
- Real **phone** on the same Wi-Fi: change `applicationUrl` in `Properties/launchSettings.json` to
  `http://0.0.0.0:5080`, then the app uses `http://<laptop IP>:5080`. Windows Firewall may ask to allow it.
- Android blocks plain `http://` by default. Banjo must add `android:usesCleartextTraffic="true"` to the `<application>` tag in `AndroidManifest.xml` for development.
