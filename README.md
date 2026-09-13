# Property Management — Rental Applications

Full-stack ASP.NET Core MVC + Razor application for a property management company: property managers maintain properties/units and review rental applications; applicants browse available units, submit applications through a multi-step wizard, and track their status through to lease issuance.

## Architecture

- `PropertyManagement` — ASP.NET Core MVC web project. `Controllers/` (MVC controllers), `Api/` (the JSON API controller), `ViewModels/` (one subfolder per feature area — `Account`, `Property`, `Unit`, `Application`, `Review`, `Portal`, `Api`, etc.), `Validations/` (shared validation logic, e.g. photo upload constraints), `Views/`, `ViewComponents/`, `wwwroot/`.
- `PropertyManagement.Domain` — entities, enums, and constants; no framework dependencies beyond ASP.NET Core Identity's base types.
- `PropertyManagement.Application` — the service-layer contracts (`IPropertyService`, `IUnitService`, `IApplicationService`, `IBlobStorageService`, etc.) plus their result/DTO types (`UnitSaveResult`, `ApplicationActionResult`, `PagedResult<T>`, ...) — this is what the Web project actually programs against.
- `PropertyManagement.Infrastructure` — `ApplicationDbContext`, EF Core migrations, the concrete service implementations (`PropertyService`, `UnitService`, `ApplicationService`, `BlobStorageService`, ...), and the startup seeders.
- `PropertyManagement.Tests` — xUnit unit tests for the service layer's business rules, run against an in-memory SQLite database (no SQL Server dependency for tests).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server LocalDB (installed with Visual Studio) or SQL Server / SQL Server Express — anything reachable via the connection string below.

## Running it locally

```
dotnet run --project PropertyManagement
```

On startup the app automatically:
1. Creates the database and applies all EF Core migrations (`Database.Migrate()`).
2. Seeds ASP.NET Identity roles (`Applicant`, `PropertyManager`).
3. Seeds demo data with [Bogus](https://github.com/bchavez/Bogus) — lookups, users, properties/units, and a rental application in every status — idempotently (safe to run repeatedly; it no-ops once data already exists).

No manual database setup or seed script is required beyond having SQL Server/LocalDB reachable.

The default dev connection string (`PropertyManagement/appsettings.Development.json`) targets LocalDB:
```
Server=(localdb)\mssqllocaldb;Database=PropertyManagement-db;Trusted_Connection=True;TrustServerCertificate=True;
```
`appsettings.json` intentionally has no `ConnectionStrings` section for non-Development environments — supply `ConnectionStrings:DefaultConnection` via an environment variable (`ConnectionStrings__DefaultConnection`), user-secrets, or an `appsettings.Production.json` you don't commit.

## Configuration

| Key | Required? | Purpose |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | **Required** | SQL Server connection string. Already set for LocalDB in `appsettings.Development.json` — see above. |
| `BlobStorage:ConnectionString` | Optional | Azure Storage connection string used to store Property/Unit photos (Portal → Properties/Units → **Photos**). |
| `BlobStorage:ContainerName` | Optional | Blob container name; defaults to `property-management-photos` and is created automatically (with public read access) on first upload. |

**Without `BlobStorage:ConnectionString` the app runs completely normally** — every page works, only actually uploading/deleting a photo fails with a clear "Azure Blob Storage connection string is not configured" message instead of the raw Azure SDK error. The Blob client is built lazily, so a missing/empty value never breaks anything outside the photo-upload actions themselves.

`appsettings.json` has no `BlobStorage` section at all (nothing to edit there, and don't add a real connection string to it). Set it via .NET user-secrets instead, run from the `PropertyManagement/` project directory (where the `.csproj` with `UserSecretsId` lives):

```
cd PropertyManagement
dotnet user-secrets set "BlobStorage:ConnectionString" "<your Azure Storage connection string>"
dotnet user-secrets list   # confirms it actually saved
```

## Seeded accounts

Every seeded account uses the password:

```
Passw0rd!1
```

| Role | Emails |
|---|---|
| Property Manager | `pm1@example.com`, `pm2@example.com` |
| Applicant | 6 accounts with Bogus-generated emails — check the `AspNetUsers` table, or just register a new Applicant/Property Manager account from the sign-up page |

The seeded data includes one rental application in each status (Draft, Submitted, Returned, Approved, Denied, Withdrawn), so logging in as `pm1@example.com` and opening **Applications** shows the full review lifecycle immediately, including a real 12-month lease issued for the Approved application.

Any logged-in account can update its own email/phone number and change its password from **Portal → Profile**.

## Running tests

```
dotnet test
```

Runs the full unit test suite (business rules in the service layer — unit availability, application status transitions, review/lease issuance, ownership checks, DB-side list filtering) against an in-memory SQLite database. No SQL Server/LocalDB instance is required for tests.

## Bonus features implemented

Of the assessment's five optional bonus items, three are implemented (review queue claim/release and PM-only notes are not):

- **Paging/sorting grid + JSON/OpenAPI endpoint** — the application list on both `Applications/Index` (Applicant) and `Review/Index` (PM) is a single reusable `ApplicationsGridViewComponent`, paged and sorted in the database, backed by `GET /api/applications` (JSON: `{ rows, totalCount }`). The endpoint is documented via .NET's built-in OpenAPI generator — with the app running, the spec is at `/openapi/v1.json`.
- **Save-with-errors per section** — the wizard's Applicant Information section now saves whatever you typed even if it fails validation (e.g. an invalid email), so nothing is lost on a reload; the Summary lists every outstanding issue across both sections, and Submit is rejected server-side while any remain.
- **Multiple applicants per application, with per-section optimistic concurrency** — an applicant can invite another Applicant-role user onto their application via email (an "Invite" button on the wizard page); both can then view and edit it. Saving Applicant Information and confirming Residence History are independently version-checked, so two co-applicants can save different sections at the same time without conflict, while a second save to the *same* section is rejected with a message to reload rather than silently overwritten.

## Notes

- Review queue claim/release and PM-only notes (the two remaining optional bonus items) were not implemented — the assessment marks these optional and evaluation is unaffected by omitting them.
