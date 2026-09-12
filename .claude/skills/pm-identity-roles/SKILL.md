---
name: pm-identity-roles
description: ASP.NET Identity setup for the Property Management app — two roles (Applicant, Property Manager), sign-up with role selection, login/logout, and enforcing permissions in both controllers and views. Use when building auth, registration, or role-based access control.
---

# Identity & Roles

## Goal

Stand up ASP.NET Identity with exactly two roles and wire role-based authorization consistently across controllers and Razor views, matching the permission matrix in `pm-overview`.

## Where this lives

- `PropertyManagement.Domain/Entities/Base/IBaseEntity.cs` — `public interface IBaseEntity { Guid Id; DateTime CreatedAt; DateTime UpdatedAt; }`, namespace `PropertyManagement.Domain.Entities.Base` (matches the folder).
- `PropertyManagement.Infrastructure/Data/ApplicationDbContext.cs` overrides `SaveChangesAsync` to auto-stamp `CreatedAt`/`UpdatedAt` on any tracked `IBaseEntity` (added → both timestamps; modified → `UpdatedAt` only) — already implemented, applies to `User` and to every future entity implementing `IBaseEntity`.
- `PropertyManagement.Domain/Entities/User.cs` — `public class User : IdentityUser<Guid>, IBaseEntity` (already created; declares its own `CreatedAt`/`UpdatedAt`, and `Id` is satisfied implicitly by the inherited `IdentityUser<Guid>.Id` — no need to redeclare it). Add domain-specific profile fields here if any, e.g. `FullName`, `Phone` — or keep those on the Applicant-info entity instead and keep `User` thin; pick one and don't duplicate. Roles use the stock `IdentityRole<Guid>` directly — no custom `Role` class. **Note:** EF Core auto-generates `Guid`-typed primary keys on `SaveChanges` by convention (`ValueGeneratedOnAdd`, confirmed in the migration snapshot) — this applies to `User.Id` and to `IdentityRole<Guid>.Id` alike, regardless of whether a constructor sets it. No manual `Id = Guid.NewGuid()` is needed anywhere, including at role-seeding time. `ApplicationDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>` (see `pm-project-setup`).
- `PropertyManagement.Infrastructure/Services/IAuthService.cs` + `AuthService.cs` — already created: all the actual auth logic (create `User`, `AddToRoleAsync`, `SignInAsync`/`PasswordSignInAsync`/`SignOutAsync`) lives here, injecting `UserManager<User>`/`SignInManager<User>`. Returns the existing Identity result types (`IdentityResult`, `SignInResult`) rather than a custom DTO — the controller already knows how to map `IdentityResult.Errors`/`SignInResult.Succeeded` to `ModelState`/redirects, so there was no reason to invent a wrapper. Registered `AddScoped<IAuthService, AuthService>()` in `Program.cs`. **Note:** `SignInManager<T>` lives in the ASP.NET Core shared framework, not in the framework-agnostic `Microsoft.Extensions.Identity.Core` that `RoleManager`/`UserManager` come from — a plain `Microsoft.NET.Sdk` class library (like Infrastructure) needs `<FrameworkReference Include="Microsoft.AspNetCore.App" />` in its `.csproj` to resolve it (already added).
- `PropertyManagement/Controllers/AccountController.cs` — already created: thin controller, constructor-injects only `IAuthService` (no direct `UserManager`/`SignInManager`/Identity dependency at all). `Register`/`Login` (GET+POST)/`Logout` (POST) each do: check `ModelState`, call the service, map the result to `View(model)` (with errors) or a redirect. Registration signs in immediately on success (no email confirmation — there's no mail infrastructure). A `returnUrl` on Login is honored only via `Url.IsLocalUrl`. Both post actions carry `[ValidateAntiForgeryToken]`.
- `PropertyManagement/Models/AccountViewModels/RegisterViewModel.cs`/`LoginViewModel.cs` — already created. `RegisterViewModel.Role` is validated server-side with `[Required] [AllowedValues(Roles.Applicant, Roles.PropertyManager)]` (built-in .NET 8+ attribute) — this is the actual server-side check against a posted role string; verified live that `Role=Admin` (or any value outside the two) is rejected with a field error and no user is created.
- `PropertyManagement/Views/Account/Register.cshtml`/`Login.cshtml` — already created; radio buttons on `Register` bind to `Roles.Applicant`/`Roles.PropertyManager` directly (`@using PropertyManagement.Domain.Constants` in the view). `PropertyManagement/Views/_ViewImports.cshtml` has `@using PropertyManagement.Models.AccountViewModels` so views can reference the view models unqualified.
- `PropertyManagement.Domain/Constants/Roles.cs` — `public static class Roles { public const string Applicant = "Applicant"; public const string PropertyManager = "PropertyManager"; public static readonly IReadOnlyList<string> All = [Applicant, PropertyManager]; }` (already created) — referenced everywhere instead of magic strings, including by the role seeder below.
- `PropertyManagement.Infrastructure/Data/Seed/IRoleSeeder.cs` + `RoleSeeder.cs` — already created and wired into `Program.cs` (registered `AddScoped<IRoleSeeder, RoleSeeder>()`, invoked in the same startup scope as `Database.Migrate()`). Idempotent via `RoleManager.RoleExistsAsync` before each `CreateAsync`; verified live (two roles created on first run, zero new inserts — confirmed via SQL log — on a second run).

> **Naming caveat:** the domain entity is named `User`, which collides with `ControllerBase.User` / Razor's `@User` (both `ClaimsPrincipal`, from the framework). Inside a controller or view, an unqualified `User` always resolves to that `ClaimsPrincipal` property, not the entity type. When you need the entity type in a controller/view, qualify it fully (`PropertyManagement.Domain.Entities.User`) or add a using alias, e.g. `using DomainUser = PropertyManagement.Domain.Entities.User;`, and declare variables as `DomainUser` there. This isn't an issue in the Infrastructure/Application layers, which have no `ClaimsPrincipal.User` in scope.

## Requirements (from spec)

- Users sign up, log in, log out.
- Sign-up lets the user specify **Applicant or Property Manager** — this is explicitly called out as "for convenience," i.e. no invite/approval flow needed, a simple radio/select on the registration form is correct.
- Permissions for each role must be reflected in **both controllers and the UI**.

## Steps

1. ~~Seed the two roles~~ — done: `RoleSeeder` (see "Where this lives" above) runs at startup via `RoleManager<IdentityRole<Guid>>`, idempotently, using the `Roles` constants.
2. ~~Register/Login/Logout, extracted into `IAuthService`~~ — done, see "Where this lives" above. Both registration paths (Applicant and Property Manager) and the invalid-role rejection have been verified live end-to-end (curl + antiforgery token + DB check: correct role assigned in `AspNetUserRoles`, `CreatedAt`/`UpdatedAt` stamped, a posted `Role` outside the allowed set creates no user). Login verified with both a wrong password (re-renders with error, no sign-in cookie) and the correct one (302 + `.AspNetCore.Identity.Application` cookie set). Logout verified (302, antiforgery-protected POST). Re-verified after the controller→service extraction — same behavior.
3. ~~`_Layout.cshtml` nav~~ — done: Login/Register links when anonymous; email + Logout form when authenticated. **Still open:** role-specific redirect targets after register/login — both roles currently land on `Home/Index` because the unit browse page (Applicant) and properties list (Property Manager) don't exist yet; revisit once `pm-properties-units` is built.
4. **Not yet done — pick up next**, once Property/Unit/Application controllers exist (`pm-properties-units`, `pm-application-wizard`, `pm-review-list`):
   - Controller-level: `[Authorize(Roles = Roles.PropertyManager)]` on Property/Unit management and Review controllers/actions; `[Authorize(Roles = Roles.Applicant)]` on application-creation actions; ownership checks (e.g. `application.ApplicantUserId == userManager.GetUserId(User)`) in every action an applicant can reach, since role alone doesn't stop one applicant from touching another's application.
   - View-level: wrap PM-only buttons/links (Add Property, Review) in `@if (User.IsInRole(Roles.PropertyManager))`, and applicant-only actions (Start Application, Withdraw) similarly — don't rely on the controller redirect alone, since a visible-but-rejected button is a worse UX and an easy review flag.

## Acceptance checks

- Registering as Applicant vs Property Manager produces a user in the correct role, verifiable via seeded/created accounts.
- An Applicant hitting a PM-only URL directly (typed in the browser) gets rejected by the controller (403 or redirect), not just hidden from nav.
- An Applicant cannot open another applicant's application by guessing its id in the URL.
- Nav and action buttons differ correctly between the two roles and the anonymous state.

## Related skills

`pm-overview` (permission matrix), `pm-project-setup` (DbContext/DI and role seeding), `pm-application-wizard` and `pm-review-list` (consume the ownership/role checks built here).
