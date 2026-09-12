---
name: pm-identity-roles
description: ASP.NET Identity setup for the Property Management app — two roles (Applicant, Property Manager), sign-up with role selection, login/logout, and enforcing permissions in both controllers and views. Use when building auth, registration, or role-based access control.
---

# Identity & Roles

## Goal

Stand up ASP.NET Identity with exactly two roles and wire role-based authorization consistently across controllers and Razor views, matching the permission matrix in `pm-overview`.

## Where this lives

- `PropertyManagement.Domain/Entities/Base/IBaseEntity.cs` — `public interface IBaseEntity { Guid Id; DateTime CreatedAt; DateTime UpdatedAt; }` (namespace is still `PropertyManagement.Domain.Entities`, only the file lives under `Entities/Base/`).
- `PropertyManagement.Domain/Entities/User.cs` — `public class User : IdentityUser<Guid>, IBaseEntity` (already created; declares its own `CreatedAt`/`UpdatedAt`, and `Id` is satisfied implicitly by the inherited `IdentityUser<Guid>.Id` — no need to redeclare it). Add domain-specific profile fields here if any, e.g. `FullName`, `Phone` — or keep those on the Applicant-info entity instead and keep `User` thin; pick one and don't duplicate. Roles use the stock `IdentityRole<Guid>` directly — no custom `Role` class. **Note:** EF Core auto-generates `Guid`-typed primary keys on `SaveChanges` by convention (`ValueGeneratedOnAdd`, confirmed in the migration snapshot) — this applies to `User.Id` and to `IdentityRole<Guid>.Id` alike, regardless of whether a constructor sets it. No manual `Id = Guid.NewGuid()` is needed anywhere, including at role-seeding time.
- `PropertyManagement.Infrastructure/Data/ApplicationDbContext.cs` — `IdentityDbContext<User, IdentityRole<Guid>, Guid>` (already created; see `pm-project-setup`).
- `PropertyManagement/Controllers/AccountController.cs` (or `Areas/Identity/...` if using Identity UI scaffolding — custom controller is simpler to control precisely per the spec's "sign-up chooses a role" requirement).
- `PropertyManagement/Views/Account/` — Register, Login views; `Models/AccountViewModels/` or similar for `RegisterViewModel`/`LoginViewModel`.
- `PropertyManagement.Domain/Constants/Roles.cs` — `public static class Roles { public const string Applicant = "Applicant"; public const string PropertyManager = "PropertyManager"; public static readonly IReadOnlyList<string> All = [Applicant, PropertyManager]; }` (already created) — referenced everywhere instead of magic strings, including by the role seeder below.
- `PropertyManagement.Infrastructure/Data/Seed/IRoleSeeder.cs` + `RoleSeeder.cs` — already created and wired into `Program.cs` (registered `AddScoped<IRoleSeeder, RoleSeeder>()`, invoked in the same startup scope as `Database.Migrate()`). Idempotent via `RoleManager.RoleExistsAsync` before each `CreateAsync`; verified live (two roles created on first run, zero new inserts — confirmed via SQL log — on a second run).

> **Naming caveat:** the domain entity is named `User`, which collides with `ControllerBase.User` / Razor's `@User` (both `ClaimsPrincipal`, from the framework). Inside a controller or view, an unqualified `User` always resolves to that `ClaimsPrincipal` property, not the entity type. When you need the entity type in a controller/view, qualify it fully (`PropertyManagement.Domain.Entities.User`) or add a using alias, e.g. `using DomainUser = PropertyManagement.Domain.Entities.User;`, and declare variables as `DomainUser` there. This isn't an issue in the Infrastructure/Application layers, which have no `ClaimsPrincipal.User` in scope.

## Requirements (from spec)

- Users sign up, log in, log out.
- Sign-up lets the user specify **Applicant or Property Manager** — this is explicitly called out as "for convenience," i.e. no invite/approval flow needed, a simple radio/select on the registration form is correct.
- Permissions for each role must be reflected in **both controllers and the UI**.

## Steps

1. ~~Seed the two roles~~ — done: `RoleSeeder` (see "Where this lives" above) runs at startup via `RoleManager<IdentityRole<Guid>>`, idempotently, using the `Roles` constants.
2. `RegisterViewModel`: email, password, confirm password, plus a required role selection (radio buttons: Applicant / Property Manager — a dropdown works too, radio reads better for a binary choice). Validate the role value server-side against the known role set (don't trust an arbitrary posted string).
3. `AccountController.Register` (POST): create the `User` via `UserManager.CreateAsync`, on success `UserManager.AddToRoleAsync` with the selected role, then sign in (`SignInManager.SignInAsync`) and redirect — applicants to the unit browse page, PMs to the properties list (or a shared dashboard, your call).
4. `AccountController.Login`/`Logout` — standard `SignInManager.PasswordSignInAsync` / `SignOutAsync` pattern.
5. Controller-level enforcement: `[Authorize(Roles = Roles.PropertyManager)]` on Property/Unit management and Review controllers/actions; `[Authorize(Roles = Roles.Applicant)]` on application-creation actions; ownership checks (e.g. `application.ApplicantUserId == User.GetUserId()`) in every action an applicant can reach, since role alone doesn't stop one applicant from touching another's application.
6. View-level enforcement: wrap PM-only buttons/links (Add Property, Review) in `@if (User.IsInRole(Roles.PropertyManager))`, and applicant-only actions (Start Application, Withdraw) similarly — don't rely on the controller redirect alone, since a visible-but-rejected button is a worse UX and an easy review flag.
7. `_Layout.cshtml` nav: show different nav items per role/auth state (Login/Register when anonymous; role-appropriate links plus Logout when authenticated).

## Acceptance checks

- Registering as Applicant vs Property Manager produces a user in the correct role, verifiable via seeded/created accounts.
- An Applicant hitting a PM-only URL directly (typed in the browser) gets rejected by the controller (403 or redirect), not just hidden from nav.
- An Applicant cannot open another applicant's application by guessing its id in the URL.
- Nav and action buttons differ correctly between the two roles and the anonymous state.

## Related skills

`pm-overview` (permission matrix), `pm-project-setup` (DbContext/DI and role seeding), `pm-application-wizard` and `pm-review-list` (consume the ownership/role checks built here).
