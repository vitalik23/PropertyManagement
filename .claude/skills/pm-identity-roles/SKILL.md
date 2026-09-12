---
name: pm-identity-roles
description: ASP.NET Identity setup for the Property Management app — two roles (Applicant, Property Manager), sign-up with role selection, login/logout, and enforcing permissions in both controllers and views. Use when building auth, registration, or role-based access control.
---

# Identity & Roles

## Goal

Stand up ASP.NET Identity with exactly two roles and wire role-based authorization consistently across controllers and Razor views, matching the permission matrix in `pm-overview`.

## Where this lives

- `PropertyManagement.Domain/` — `ApplicationUser : IdentityUser` (add domain-specific profile fields here if any, e.g. `FullName`, `Phone` — or keep those on the Applicant-info entity instead and keep `ApplicationUser` thin; pick one and don't duplicate).
- `PropertyManagement.Infrastructure/Data/ApplicationDbContext.cs` — `IdentityDbContext<ApplicationUser>` (see `pm-project-setup`).
- `PropertyManagement/Controllers/AccountController.cs` (or `Areas/Identity/...` if using Identity UI scaffolding — custom controller is simpler to control precisely per the spec's "sign-up chooses a role" requirement).
- `PropertyManagement/Views/Account/` — Register, Login views; `Models/AccountViewModels/` or similar for `RegisterViewModel`/`LoginViewModel`.
- Role constants: a small static class (`Roles.Applicant`, `Roles.PropertyManager`) referenced everywhere instead of magic strings.

## Requirements (from spec)

- Users sign up, log in, log out.
- Sign-up lets the user specify **Applicant or Property Manager** — this is explicitly called out as "for convenience," i.e. no invite/approval flow needed, a simple radio/select on the registration form is correct.
- Permissions for each role must be reflected in **both controllers and the UI**.

## Steps

1. Seed two roles via `RoleManager<IdentityRole>` at startup (covered in `pm-project-setup`'s seeding step) — role names should match the `Roles` constants exactly.
2. `RegisterViewModel`: email, password, confirm password, plus a required role selection (radio buttons: Applicant / Property Manager — a dropdown works too, radio reads better for a binary choice). Validate the role value server-side against the known role set (don't trust an arbitrary posted string).
3. `AccountController.Register` (POST): create the `ApplicationUser` via `UserManager.CreateAsync`, on success `UserManager.AddToRoleAsync` with the selected role, then sign in (`SignInManager.SignInAsync`) and redirect — applicants to the unit browse page, PMs to the properties list (or a shared dashboard, your call).
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
