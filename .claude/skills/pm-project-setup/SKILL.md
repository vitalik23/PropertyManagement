---
name: pm-project-setup
description: Wire up the empty PropertyManagement .NET 10 scaffold — NuGet packages, EF Core DbContext and code-first migrations against SQL Server, DB create/migrate on startup, and the idempotent Bogus seeding pipeline (lookups, property managers, applicants, properties, units, applications in every status). Use before building any feature, or when setting up the database/seed data.
---

# Project Setup: Packages, DbContext, Migrations, Seeding

## Goal

Turn the bare 4-project scaffold into a runnable app with a real database: packages installed in the right project, a `DbContext`, code-first migrations, automatic create/migrate on startup, and idempotent Bogus-based seed data covering every lookup value and every application status.

## Where this lives

- `PropertyManagement.Domain/Entities/` — all entities live here, including `User` (Identity) and `IBaseEntity`. Domain-only POCOs and enums, no EF references (the `User`/`IdentityUser<Guid>` dependency on `Microsoft.Extensions.Identity.Stores` is the one exception, already installed).
- `PropertyManagement.Domain/Entities/Base/IBaseEntity.cs` — `public interface IBaseEntity { Guid Id { get; set; } DateTime CreatedAt { get; set; } DateTime UpdatedAt { get; set; } }` (already created; namespace is still `PropertyManagement.Domain.Entities`, only the file is nested under `Entities/Base/`). It's a plain contract with no shared implementation, so each implementer declares its own `Id`/`CreatedAt`/`UpdatedAt` (or gets them from its own common base class — see the open question below). `User : IdentityUser<Guid>, IBaseEntity` already implements it: `Id` comes for free from `IdentityUser<Guid>`, only `CreatedAt`/`UpdatedAt` are declared directly, since `User` can't also inherit an abstract base class (no multiple class inheritance in C#). Set `CreatedAt`/`UpdatedAt` yourself on save (e.g. a `SaveChangesAsync` override on `ApplicationDbContext` that stamps both on add and only `UpdatedAt` on modify) rather than relying on DB defaults.
  - **Open question for whoever implements step 2 below:** plain (non-Identity) entities like `Property`/`Unit`/etc. aren't constrained by `User`'s multiple-inheritance problem — they could each implement `IBaseEntity` directly (more repetition) or share a small abstract `BaseEntity : IBaseEntity` class to inherit `Id`/`CreatedAt`/`UpdatedAt` for free (less repetition, one extra type). Either is fine; pick one and use it consistently across all the entities added in step 2.
- `PropertyManagement.Infrastructure/` — `Data/ApplicationDbContext.cs`, `Data/Migrations/`, `Data/Seed/` (Bogus seeders), Identity configuration, EF package references.
- `PropertyManagement.Application/` — service interfaces/implementations that depend on Domain, consumed by the web project (or fold services into Infrastructure if you don't need the extra layer — stay consistent either way).
- `PropertyManagement/` — `Program.cs` (DI wiring, `app.Run()`), `appsettings.json` (connection string).

## Status — already done

DB connection + Identity infrastructure is wired up: `Microsoft.EntityFrameworkCore.SqlServer`/`.Tools`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (Infrastructure), `Microsoft.EntityFrameworkCore.Design` (web) and `Microsoft.Extensions.Identity.Stores` (Domain) are installed; `PropertyManagement.Domain/Entities/User.cs` (`User : IdentityUser<Guid>, IBaseEntity`), `PropertyManagement.Domain/Entities/Base/IBaseEntity.cs` (`Id`/`CreatedAt`/`UpdatedAt` contract for domain entities), and `PropertyManagement.Infrastructure/Data/ApplicationDbContext.cs` (`IdentityDbContext<User, IdentityRole<Guid>, Guid>`) exist; `Program.cs` has `AddDbContext<ApplicationDbContext>`, `AddIdentity<User, IdentityRole<Guid>>()`, `UseAuthentication()`, and a `Database.Migrate()` call before `app.Run()`; the dev connection string lives in `appsettings.Development.json` (`Database=PropertyManagement-db`, LocalDB); migrations `InitialIdentity` and `AddAuditFieldsToUser` are applied and verified (`AspNetUsers.Id`/`AspNetRoles.Id` are `uniqueidentifier`; `AspNetUsers` has `CreatedAt`/`UpdatedAt` `datetime2` columns) via `dotnet run`. Role seeding is also done: `PropertyManagement.Domain/Constants/Roles.cs` (`Roles.Applicant`, `Roles.PropertyManager`, `Roles.All`) plus `PropertyManagement.Infrastructure/Data/Seed/IRoleSeeder.cs`/`RoleSeeder.cs`, registered `AddScoped<IRoleSeeder, RoleSeeder>()` and invoked in `Program.cs`'s startup scope right after `db.Database.Migrate()`; verified idempotent live (two roles inserted on first run, zero inserts on a second run, still exactly two rows in `AspNetRoles`).

**Note:** EF Core auto-generates `Guid`-typed primary keys on `SaveChanges` by convention (`ValueGeneratedOnAdd`, confirmed in the migration snapshot) — this applies to `User.Id` and to `IdentityRole<Guid>.Id` alike. No manual `Id = Guid.NewGuid()` is needed anywhere, including at role-seeding time (step 5 below).

**Still to do** (this skill's remaining scope): Bogus, the domain `DbSet<T>`s, role/user/lookup/property/application seeding, and the production connection-string story. Don't redo the steps above — pick up from "Packages to add" below for what's left.

## Packages to add

- `PropertyManagement.Infrastructure`: `Bogus` (EF Core/Identity packages already installed — see Status above).
- Pin package versions to match the `net10.0` TFM (use whatever the current stable release is at install time — don't hardcode an old major version).

## Steps

1. ~~Connection string, `User`/`IBaseEntity`/`ApplicationDbContext`, `Program.cs` DI + `UseAuthentication()` + `Database.Migrate()`, `InitialIdentity` + `AddAuditFieldsToUser` migrations~~ — done, see Status above.
2. **Domain entities + `DbSet<T>`** — add `Property`, `Unit`, `UnitType` (lookup), `Lease`, `RentalApplication`, `ApplicationStatusHistory`, `Residence` to `PropertyManagement.Domain/Entities/`, each implementing `IBaseEntity` for `Id`/`CreatedAt`/`UpdatedAt` (directly, or via a shared abstract base class — see the open question above), then register each as a `DbSet<T>` on `ApplicationDbContext` (plus anything bonus features add later — `ReviewClaim`, `PropertyManagerNote`, `ApplicationApplicant` join — only if you're doing that bonus). Follow up with `dotnet ef migrations add <Name> --project PropertyManagement.Infrastructure --startup-project PropertyManagement` per schema change; `Program.cs`'s existing `Database.Migrate()` call applies them on next run — no extra wiring needed.
3. ~~Role seeding~~ — done, see Status above. **Remaining part of this step:** register any further Application/Infrastructure services in `Program.cs` (`AddScoped<IXxxService, XxxService>`) as they're built (`RoleSeeder` is the pattern to follow — small `IXxxSeeder`/`XxxSeeder` pair in `Infrastructure/Data/Seed/`, invoked from the same startup scope as `Database.Migrate()`).
4. **Production connection string** — `appsettings.json` still has no `ConnectionStrings` section by design (dev-only value lives in `appsettings.Development.json`); decide how prod supplies `DefaultConnection` (env var, user-secrets, Azure config) when that need arises, and document it in the README.
5. **Seeding — must be idempotent** (safe to run on every startup, e.g. check `if (!context.Properties.Any())` guards per entity group, or check-and-skip per named seed record):
   - ~~Roles~~ — done (`RoleSeeder`, see Status above).
   - Property managers and applicants: seed `User` + role assignment via `UserManager`/`RoleManager` (use `Roles.Applicant`/`Roles.PropertyManager`), not raw `DbContext.Add` (so Identity's password hashing etc. runs correctly). Use a fixed, documented seed password for demo accounts (put it in the README, not committed as a "secret").
   - Lookups: `UnitType` values, include at least one **Inactive** one so the Active/Inactive rule (see `pm-properties-units`) has something to exercise.
   - Properties and units: use Bogus (`Faker<Property>`, `Faker<Unit>`) for realistic names/addresses/rents; keep unit `UnitTypeId` pointing at seeded lookups only.
   - Applications: seed **at least one application in every status** (Draft, Submitted, Returned, Approved, Denied, Withdrawn) so the reviewer can see the full lifecycle without manually creating each state. An Approved one needs a matching Lease row with a 12-month term covering "today" so the unit-unavailable rule is demonstrably exercised.
   - Wrap the whole seed in idempotency checks keyed on stable identifiers (e.g. a fixed seed email per demo user, a fixed property name) — re-running `Program.cs` on every `dotnet run` must not create duplicates.
   - Call the seeder from `Program.cs` right after the existing `db.Database.Migrate()` call, inside the same DI scope.

## Acceptance checks

- `dotnet run` on a machine with only SQL Server/LocalDB installed creates the database, applies all migrations, and seeds data with no manual steps.
- Running the app twice in a row does not duplicate seed rows (check `SELECT COUNT(*)` on a seeded table stays constant across restarts).
- At least one unit is seeded with an Inactive `UnitType` still assigned to it.
- Applications exist in all six statuses; at least one Approved application has a Lease whose date range covers today.

## Related skills

`pm-overview` (index), `pm-identity-roles` (User/Role model consumed by the DbContext), `pm-testing` (test project setup mirrors this project layout).
