# Mine Safety API

The backend for the mine safety reporting app (ITMDA3 Deliverable 4).
ASP.NET Core Web API (.NET 9) + EF Core + SQL Server + JWT login.

**Built so far:** login, Locations (full CRUD), Accounts (full CRUD), Roles list.
**Next:** Reports (submit, list, view, photo upload, supervisor review), Notifications.

---

## 1. How to run it

1. Open `MineSafetyApi.csproj` in Visual Studio (or the folder in VS Code).
2. Press **F5** (or run `dotnet run` in a terminal in this folder).
   The first run downloads the packages, which takes a minute.
3. Your browser opens on **http://localhost:5080/swagger**.

On first run the app creates a database called `MineSafetyDb` in **SQL Server LocalDB**
(installed with Visual Studio) and fills it with test data. If LocalDB isn't installed,
change the connection string in `appsettings.json` to point at your SQL Server.

### Test accounts (password for all: `Password123!`)

| Worker ID | Role |
|---|---|
| W1001 | Worker |
| S2001 | Supervisor |
| O3001 | SafetyOfficer |
| A9001 | Administrator |

### Testing in Swagger

1. Open `POST /api/auth/login` > **Try it out** > send `{ "login": "A9001", "password": "Password123!" }`
2. Copy the `token` value from the response (without the quotes).
3. Click the **Authorize** button at the top, paste the token, click Authorize.
4. Now every other endpoint works as that user. Try `GET /api/locations`.
5. To test permissions, log in as `W1001` instead and try `POST /api/locations`: you get **403**.

---

## 2. The folders (what lives where)

| Folder | What it is | Analogy |
|---|---|---|
| `Program.cs` | Start-up: registers services, sets the order requests flow through | Opening the shop in the morning |
| `Controllers/` | Receive HTTP requests, call a service, return a status code | The receptionist |
| `Services/` | The actual rules and work (checks, saving, hashing) | The person doing the job |
| `Dtos/` | Shapes of the JSON going in and out | The forms people fill in |
| `Entities/` | C# classes that mirror the database tables (from the D3 ERD) | The filing cabinet layout |
| `Data/` | `MineSafetyDbContext` (EF Core's link to the DB) and the test-data seeder | The filing clerk |
| `Common/` | Small shared helpers (role names, ServiceResult) | |

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
- **JWT:** after login the app gets a signed token holding the account ID and role. It sends it
  with every request as the header `Authorization: Bearer <token>`. Lasts 12 hours (one shift).
  "Logout" = the app deletes its saved token.

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
| POST | `/api/accounts` | admin | `{ workerId, fullName, email, password, roleId }` | 201 / 400 / 409 |
| PUT | `/api/accounts/{id}` | admin | `{ fullName, email, roleId, isActive, newPassword? }` | 200 / 400 / 404 / 409 |
| DELETE | `/api/accounts/{id}` | admin | | 204 / 400 / 404 (soft delete) |
| GET | `/api/roles` | admin | | 200 list (for the role dropdown) |

---

## 6. Things to agree with Michael

- The entities in `Entities/` follow the D3 ERD exactly. In development the app **creates the
  tables itself** (`EnsureCreated`). If Michael builds the database by script instead, the table
  and column names must match these classes.
- Two columns were **added to Reports** that the ERD is missing: `ClientReportId` (stops duplicate
  uploads when the phone retries) and `OccurredAt` (when the event happened underground).
- The ERD has both `Accounts.RoleID` and an `AccountRoles` table. We use **`Accounts.RoleID` only**.
- `AccountLocations` (supervisor areas) and `AccountRoles` are left out for the prototype.
- ERD typo: `AttachementID` is `AttachmentId` here.

## 7. Connecting the phone later

`localhost` only works on the laptop itself. For Banjo's phone or emulator:
- Android **emulator**: use `http://10.0.2.2:5080` from the app.
- Real **phone** on the same Wi-Fi: change `applicationUrl` in `Properties/launchSettings.json` to
  `http://0.0.0.0:5080`, then the app uses `http://<laptop IP>:5080`. Windows Firewall may ask to allow it.
- Android blocks plain `http://` by default. Banjo must add `android:usesCleartextTraffic="true"` to the `<application>` tag in `AndroidManifest.xml` for development.
