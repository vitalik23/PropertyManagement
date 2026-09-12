---
name: pm-overview
description: Entry point for the Property Management rental-application technical assessment — role/permission matrix, full requirements index pointing at the other pm-* skills, and the deliverables checklist (repo, README, video). Use when starting or getting oriented on this assessment, or asking "what's left to build".
---

# Property Management Technical Assessment — Overview

## Goal

Give a single orientation point for the whole assessment: what the two roles can do, which `pm-*` skill covers which requirement, and what "done" means for delivery. Read this first; then jump to the specific skill for the area you're building.

## The system in one paragraph

A property manager maintains properties and their units and reviews submitted applications; approval issues a 12-month lease. An applicant browses available units, fills out and submits a rental application (Applicant Info + Residence History + Summary), and can withdraw it or correct/resubmit one that was returned. Built with ASP.NET Core MVC + Razor (.NET 10), EF Core code-first against SQL Server, ASP.NET Identity, and Bogus-seeded idempotent sample data.

## Current scaffold (ground truth — don't re-derive, just use)

`C:\WorkProjects\Olexa\Clients Projects\PropertyManagement\` is a Clean Architecture skeleton, empty beyond the default MVC template:

- `PropertyManagement.slnx` — solution file, four projects wired by project references only
- `PropertyManagement/` — the web/MVC project (`net10.0`, `Microsoft.NET.Sdk.Web`). Only `Controllers/HomeController.cs`, `Models/ErrorViewModel.cs`, default `Views/`, template `Program.cs`. No DbContext/Identity DI, no connection string.
- `PropertyManagement.Domain/` — entities live here. Currently empty (just the `.csproj`).
- `PropertyManagement.Application/` — application/business logic (services, view models can live here or in the web project — decide once, stay consistent). Currently empty.
- `PropertyManagement.Infrastructure/` — EF Core `DbContext`, migrations, Identity configuration, repository/service implementations. Currently empty.

No NuGet packages are referenced anywhere yet. No git repo. No README. No test project.

## Role / permission matrix

| Action | Applicant | Property Manager |
|---|---|---|
| Sign up, log in, log out | ✅ | ✅ (role chosen at sign-up) |
| Add/edit/remove properties & units | ❌ | ✅ |
| Browse available units | ✅ | — |
| Start / fill / submit / withdraw an application | ✅ (own applications only) | — |
| Correct and resubmit a Returned application | ✅ (own) | — |
| Review a submitted application (Approve/Return/Deny) | ❌ | ✅ |
| View application list | own applications only | all applications |
| View status/review history on an application | — | ✅ |

Enforce every row in **both** the controller (`[Authorize(Roles=...)]` + explicit ownership checks) and the view (hide actions the current user can't take) — the spec calls this out explicitly, and it's an easy place to lose points by doing only one side.

## Requirements index → which skill covers it

| Spec section | Skill |
|---|---|
| Tech Requirements 2 (.NET 10, Identity, DB create+migrate on start, idempotent Bogus seeding) | `pm-project-setup` |
| Tech Requirements 2a (ASP.NET Identity) + Functional 1 (sign up/login/logout, role choice) | `pm-identity-roles` |
| Functional 2 (Properties & Units, modals, Unit Type lookup Active/Inactive, availability) | `pm-properties-units` |
| Functional 3d (approval → 12-month lease, unit unavailable while leased) | `pm-properties-units` (lease/availability rule) + `pm-review-list` (issuing the lease) |
| Functional 4 (Rental Application wizard: sections, Continue/Back/Submit, Residence History modal, Draft/Returned editable) | `pm-application-wizard` |
| Functional 5 (Review modal, statuses, status/review history) | `pm-review-list` |
| Functional 6 (Application List, DB-side filtering, scoping) | `pm-review-list` |
| Tech Requirements 2c (unit tests for business logic) | `pm-testing` |
| Bonus 1–5 | `pm-bonus-features` |

## Deliverables checklist

- [ ] GitHub repository (public or with reviewer access) — `git init` this scaffold, commit as you go.
- [ ] `README.md` at repo root: setup/installation steps **beyond the framework standard** — SQL Server connection string setup, `dotnet ef database update` (if not auto-applied), any local secrets/config, how to log in as seeded Applicant/PM accounts, how to run tests.
- [ ] Video recording demonstrating the app running locally and covering every requirement in this table (both roles, the full application lifecycle from Draft through a terminal status, unit availability rules, permission boundaries).
- [ ] Delivered within 3 days of the assessment date, or an earlier heads-up to the client if more time is needed.

## Related skills

`pm-project-setup`, `pm-identity-roles`, `pm-properties-units`, `pm-application-wizard`, `pm-review-list`, `pm-testing`, `pm-bonus-features`.
