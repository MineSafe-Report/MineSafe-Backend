# MineSafe API

Backend API for the MineSafe reporting application. Built with ASP.NET Core 9, Entity Framework Core, SQL Server, JWT authentication, and Swagger.

## Requirements

- .NET 9 SDK or Visual Studio with ASP.NET development tools
- SQL Server LocalDB or another SQL Server instance
- `sqlcmd` or SQL Server Management Studio (SSMS)

## Database setup

The API does not create or alter database tables. Create a database named `MineSafetyDb`, then run the files in `database/schema` in this order:

1. `Roles.sql`
2. `IncidentTypes.sql`
3. `ReportStatuses.sql`
4. `Locations.sql`
5. `Accounts.sql`
6. `AccountRoles.sql`
7. `AccountLocations.sql`
8. `Reports.sql`
9. `ReportAttachments.sql`
10. `ReportReviews.sql`
11. `ReportAuditLog.sql`
12. `Notifications.sql`

For an existing database, review and apply the required files in `database/migrations` before starting the API.

## Run the API

```powershell
dotnet run
```

Swagger is available at [http://localhost:5080/swagger](http://localhost:5080/swagger).

In the Development environment, empty tables are populated with test data by `Data/DbSeeder.cs`.

## Development accounts

All development accounts use the password `Password123!`.

| Worker ID | Role | Assigned locations |
|---|---|---|
| `W1001` | Worker | Tunnel 7, Stope 12B |
| `S2001` | Supervisor | Tunnel 7, Stope 12B |
| `S2002` | Supervisor | Shaft 1 Station, Workshop |
| `O3001` | Safety Officer | All locations |
| `A9001` | Administrator | All locations |

To test authenticated endpoints in Swagger:

1. Call `POST /api/auth/login` using a development account.
2. Copy the returned token.
3. Select **Authorize** and enter the token.

## Configuration

The default development connection string is stored in `appsettings.Development.json`.

For non-development environments, provide a secure JWT key through the `Jwt__Key` environment variable. Do not commit production secrets.

## Tests

```powershell
dotnet test tests/MineSafetyApi.Tests
```

The automated tests use an in-memory database and do not require SQL Server.

Database validation scripts are available in `database/tests`.

## Android development

Start the API for devices on the local network:

```powershell
dotnet run --launch-profile lan
```

- Android emulator: `http://10.0.2.2:5080`
- Physical device on the same network: `http://<computer-ip>:5080`

Android must allow clear-text HTTP traffic during local development. See `docs/android-integration.md` for the complete integration guide and `docs/ANDROID_API_CONTRACT.md` for the API contract.

## Main features

- JWT authentication with role and location-based access
- Account and location management
- Offline-safe report submission and attachment retries
- Supervisor review and automatic approval rules
- Safety Officer incident access
- Audit history and notifications
- Soft deletion for records that must retain history

Use Swagger for the current endpoint list and request/response formats.
