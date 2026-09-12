---
name: pm-project-setup
description: Wire up the empty PropertyManagement .NET 10 scaffold — NuGet packages, EF Core DbContext and code-first migrations against SQL Server, DB create/migrate on startup, and the idempotent Bogus seeding pipeline (lookups, property managers, applicants, properties, units, applications in every status). Use before building any feature, or when setting up the database/seed data.
---

# Project Setup: Packages, DbContext, Migrations, Seeding

## Goal

Turn the bare 4-project scaffold into a runnable app with a real database: packages installed in the right project, a `DbContext`, code-first migrations, automatic create/migrate on startup, and idempotent Bogus-based seed data covering every lookup value and every application status.

## Where this lives

- `PropertyManagement.Domain/` — entities (POCOs), enums, no EF/Identity references.
- `PropertyManagement.Infrastructure/` — `Data/ApplicationDbContext.cs`, `Data/Migrations/`, `Data/Seed/` (Bogus seeders), Identity configuration, EF package references.
- `PropertyManagement.Application/` — service interfaces/implementations that depend on Domain, consumed by the web project (or fold services into Infrastructure if you don't need the extra layer — stay consistent either way).
- `PropertyManagement/` — `Program.cs` (DI wiring, `app.Run()`), `appsettings.json` (connection string).

## Packages to add (none exist yet — check before assuming any are already there)

- `PropertyManagement.Infrastructure`: `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Tools`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Bogus`.
- `PropertyManagement`: `Microsoft.EntityFrameworkCore.Design` (needed at the startup project for `dotnet ef`), `Microsoft.AspNetCore.Identity.UI` if using scaffolded Identity pages (optional — you're building custom auth views per `pm-identity-roles`, so this may not be needed).
- Pin package versions to match the `net10.0` TFM (use whatever the current stable EF Core 10.x / Identity releases are at install time — don't hardcode an old major version).

## Steps

1. **Connection string** — add to `appsettings.json` (and `appsettings.Development.json` for local overrides): `"DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=PropertyManagement;Trusted_Connection=True;MultipleActiveResultSets=true"` (adjust for SQL Server Express if that's the target — document the exact expectation in the README per `pm-overview`).
2. **DbContext** — `ApplicationDbContext : IdentityDbContext<ApplicationUser>` in Infrastructure (see `pm-identity-roles` for the user/role model). `DbSet<T>` for every entity: Property, Unit, UnitType (lookup), Lease, RentalApplication, ApplicationStatusHistory, Residence, and anything bonus features add later (ReviewClaim, PropertyManagerNote, ApplicationApplicant join, etc. — only add these if you're doing the corresponding bonus).
3. **Program.cs**:
   - `builder.Services.AddDbContext<ApplicationDbContext>(opt => opt.UseSqlServer(connectionString))`
   - `builder.Services.AddIdentity<ApplicationUser, IdentityRole>(...)` (full setup in `pm-identity-roles`)
   - Register Application/Infrastructure services (`AddScoped<IXxxService, XxxService>`)
   - After `var app = builder.Build();` and before `app.Run()`: open a scope, resolve `ApplicationDbContext`, call `Database.Migrate()` (this both creates the DB and applies migrations — don't use `EnsureCreated()`, it's incompatible with migrations), then call the seeder.
4. **Migrations** — `dotnet ef migrations add InitialCreate --project PropertyManagement.Infrastructure --startup-project PropertyManagement`, repeat per entity/schema change. Keep migrations in Infrastructure, run `dotnet ef database update` (or rely on the startup `Database.Migrate()`) — document which one the reviewer should rely on in the README.
5. **Seeding — must be idempotent** (safe to run on every startup, e.g. check `if (!context.Properties.Any())` guards per entity group, or check-and-skip per named seed record):
   - Lookups first: `UnitType` values, include at least one **Inactive** one so the Active/Inactive rule (see `pm-properties-units`) has something to exercise.
   - Property managers and applicants: seed `ApplicationUser` + role assignment via `UserManager`/`RoleManager`, not raw `DbContext.Add` (so Identity's password hashing etc. runs correctly). Use a fixed, documented seed password for demo accounts (put it in the README, not committed as a "secret").
   - Properties and units: use Bogus (`Faker<Property>`, `Faker<Unit>`) for realistic names/addresses/rents; keep unit `UnitTypeId` pointing at seeded lookups only.
   - Applications: seed **at least one application in every status** (Draft, Submitted, Returned, Approved, Denied, Withdrawn) so the reviewer can see the full lifecycle without manually creating each state. An Approved one needs a matching Lease row with a 12-month term covering "today" so the unit-unavailable rule is demonstrably exercised.
   - Wrap the whole seed in idempotency checks keyed on stable identifiers (e.g. a fixed seed email per demo user, a fixed property name) — re-running `Program.cs` on every `dotnet run` must not create duplicates.

## Acceptance checks

- `dotnet run` on a machine with only SQL Server/LocalDB installed creates the database, applies all migrations, and seeds data with no manual steps.
- Running the app twice in a row does not duplicate seed rows (check `SELECT COUNT(*)` on a seeded table stays constant across restarts).
- At least one unit is seeded with an Inactive `UnitType` still assigned to it.
- Applications exist in all six statuses; at least one Approved application has a Lease whose date range covers today.

## Related skills

`pm-overview` (index), `pm-identity-roles` (User/Role model consumed by the DbContext), `pm-testing` (test project setup mirrors this project layout).
