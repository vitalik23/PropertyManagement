---
name: pm-testing
description: Set up the xUnit test project and cover the assessment's business logic — status transitions, lease/availability rules, section validation, and permission/ownership checks. Use when adding unit tests or setting up the test project.
---

# Unit Testing

## Goal

Satisfy "Add unit tests for business logic" with a real test project exercising the rules that actually matter for correctness, not incidental coverage of controllers/views.

## Where this lives

- New project: `PropertyManagement.Tests/` (add to `PropertyManagement.slnx`), referencing `PropertyManagement.Domain` and `PropertyManagement.Application` (and `Infrastructure` only if a test needs the EF Core InMemory or Sqlite provider to exercise a query-shaped rule like DB-side filtering).
- Packages: `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, `Microsoft.EntityFrameworkCore.InMemory` (or `Microsoft.Data.Sqlite` if a test needs real SQL translation behavior — InMemory doesn't validate `Where` clauses translate to SQL, so prefer Sqlite in-memory for the "filtering done in the database" check if you want to actually assert that).
- Test files mirror the source structure: `Services/UnitAvailabilityServiceTests.cs`, `Services/ApplicationReviewServiceTests.cs`, `Wizard/RentalApplicationViewModelValidationTests.cs`, `Auth/OwnershipTests.cs` (or per-controller test classes if the ownership checks live there).

## What to cover (mapped to spec rules, not just "write some tests")

- **Unit availability** (`pm-properties-units`): a unit with a lease whose range covers today is unavailable; a unit with a lease entirely in the past or future is available; boundary dates (lease starts/ends exactly today) resolve correctly per whatever inclusive/exclusive convention you pick — pin that convention down with a test.
- **Second-lease prevention** (`pm-review-list`): approving an application for a unit that already has a covering lease is rejected; approving when the unit is free succeeds and creates a lease with `EndDate == StartDate.AddMonths(12)`.
- **Status transitions**: only Submitted → {Returned, Denied, Approved} are valid review outcomes; Draft/Returned are the only statuses from which the applicant wizard allows edits; Approved/Denied/Withdrawn are terminal — attempting any transition out of a terminal status should be rejected wherever that's enforced (service layer, not just UI).
- **Submit-time active-lease check** (`pm-application-wizard`): submitting when the unit already has a covering lease is rejected without affecting other applications for the same unit.
- **Section validation** (`pm-application-wizard`): Continue with an invalid current section does not persist and does not advance; a valid section persists and advances.
- **Unit Type Active/Inactive enforcement** (`pm-properties-units`): assigning an Inactive type to a new/changing unit is rejected; keeping an already-assigned Inactive type on an edit that doesn't change the type succeeds.
- **Ownership/permission checks** (`pm-identity-roles`): an applicant cannot act on another applicant's application; a non-PM cannot reach review/property-management operations — test at the service/authorization-logic level, or via `WebApplicationFactory` integration tests against the controllers if you'd rather cover it there.
- If any bonus features are implemented (`pm-bonus-features`), especially item 5's optimistic concurrency: a stale second save is rejected, not silently overwritten — this is exactly the kind of business rule unit tests are for.

## Steps

1. `dotnet new xunit -o PropertyManagement.Tests`, add to the `.slnx`, add project references.
2. Favor testing service-layer methods (`IUnitAvailabilityService`, `IApplicationReviewService`, wizard section-save logic) directly over testing through MVC controllers — faster, more focused, and keeps controllers thin enough that there's little controller-specific logic left to test separately.
3. Use EF Core Sqlite in-memory (`UseSqlite("DataSource=:memory:")`, keep the connection open for the test's lifetime) when a test needs to prove a `Where` clause is actually filtering in the database, since the EF InMemory provider silently accepts LINQ that wouldn't translate to real SQL.
4. Arrange test data directly against the `DbContext` (no need for Bogus in tests — use small, explicit, deterministic fixtures so failures are easy to read).

## Acceptance checks

- `dotnet test` from the repo root runs the whole suite with no manual setup beyond what's already true for `dotnet run` (no live SQL Server dependency — Sqlite/InMemory only).
- Every rule listed above has at least one passing test and one test proving the rejection path (not just the happy path).

## Related skills

`pm-project-setup` (project referencing pattern), `pm-properties-units`, `pm-application-wizard`, `pm-review-list`, `pm-identity-roles` (the business logic under test lives in these).
