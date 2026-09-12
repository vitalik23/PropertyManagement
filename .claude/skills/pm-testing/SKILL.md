---
name: pm-testing
description: Set up the xUnit test project and cover the assessment's business logic — status transitions, lease/availability rules, section validation, and permission/ownership checks. Use when adding unit tests or setting up the test project.
---

# Unit Testing

## Status: done and verified

`PropertyManagement.Tests` (xUnit, referencing `PropertyManagement.Infrastructure`) covers every rule listed below with both a happy-path and a rejection-path test. `dotnet test` from the repo root: 34/34 passing, no SQL Server/LocalDB dependency — each test spins up its own `SqliteConnection("DataSource=:memory:")` (kept open for the test's lifetime) via `TestDbContextFactory`, with deterministic fixtures built by `TestDataBuilder`. Boundary convention pinned down by test: `UnitAvailabilityService.IsAvailableAsync`'s `StartDate <= today && EndDate >= today` is inclusive on **both** ends — a lease starting or ending exactly today makes the unit unavailable.

## Goal

Satisfy "Add unit tests for business logic" with a real test project exercising the rules that actually matter for correctness, not incidental coverage of controllers/views.

## Where this lives

- `PropertyManagement.Tests/` (added to `PropertyManagement.slnx`), referencing only `PropertyManagement.Infrastructure` (Domain/Application come along transitively — the services and `ApplicationDbContext` under test live in Infrastructure).
- Packages beyond the `dotnet new xunit` defaults (`Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, `coverlet.collector`): `Microsoft.EntityFrameworkCore.Sqlite` — chosen over the InMemory provider specifically because InMemory silently accepts LINQ that wouldn't translate to real SQL, which matters for `GetFilteredApplicationsAsync`'s DB-side filtering claim.
- `TestDbContextFactory.cs` — opens a `SqliteConnection("DataSource=:memory:")` per test (kept open for the test's lifetime, since an in-memory Sqlite DB disappears when its one connection closes), builds an `ApplicationDbContext` on it, `Database.EnsureCreated()`. Each test class instantiates its own factory in the constructor and disposes it in `Dispose()` (`IDisposable` per-test isolation, no shared state between tests).
- `TestDataBuilder.cs` — static async helpers (`CreatePropertyAsync`, `CreateUnitTypeAsync`, `CreateUnitAsync`, `CreateUserAsync`, `CreateApplicationAsync`, `CreateLeaseAsync`) that add small, explicit, deterministic fixtures directly to the context and save — no Bogus, so a failing test is easy to read.
- Test files mirror the service they cover: `Services/UnitAvailabilityServiceTests.cs`, `Services/ApplicationReviewServiceTests.cs`, `Services/ApplicationServiceTests.cs`, `Services/UnitServiceTests.cs`.

## What's covered (mapped to spec rules)

- **Unit availability** (`pm-properties-units`) — `UnitAvailabilityServiceTests`: covering lease → unavailable; lease entirely past/future → available; lease starting exactly today → unavailable; lease ending exactly today → unavailable (pins the inclusive-both-ends convention); `GetAvailableUnitsAsync` excludes the leased unit and includes the free one.
- **Second-lease prevention** (`pm-review-list`) — `ApplicationReviewServiceTests`: Approve on a free unit creates a `Lease` with `EndDate == StartDate.AddMonths(12)` and sets `Approved`; Approve on a unit with an existing covering lease fails with "This unit already has an active lease." and no second `Lease` row is created.
- **Review status transitions** (`pm-review-list`) — reviewing a non-`Submitted` application (e.g. `Draft`) fails with "Only submitted applications can be reviewed."; Return/Deny without a comment fail and leave the status unchanged; Return/Deny with a comment set the expected status and write an `ApplicationStatusHistory` row with `FromStatus`/`ToStatus`/`Comment`/`ChangedByUserId`.
- **Submit-time rules** (`pm-application-wizard`) — `ApplicationServiceTests.SubmitAsync_*`: Draft with both sections complete and an available unit succeeds and writes history; incomplete sections fail without changing status; an already-leased unit fails with "This unit is no longer available."; an already-`Submitted` application fails with "This application can no longer be edited."
- **Withdraw terminality** — `WithdrawAsync_FromNonTerminalStatus_Succeeds`/`WithdrawAsync_FromTerminalStatus_Fails` theories: Draft/Submitted/Returned succeed and become `Withdrawn`; Approved/Denied/Withdrawn each fail and stay unchanged.
- **Ownership checks** (`pm-identity-roles`) — `SaveApplicantInfoAsync_WrongOwner_Fails`, `AddResidenceAsync_WrongOwner_Fails`: a non-owning `applicantUserId` is rejected and the underlying data is untouched. Both routes go through the shared `GetEditableOwnedAsync`/`IsEditableOwned` check, so this exercises the one code path shared by every mutating method on `ApplicationService`.
- **DB-side filtering** (`pm-review-list`) — `GetFilteredApplicationsAsync_*`: no filters returns everything; status/property/applicant filters each narrow correctly alone and combined. Run against real Sqlite (not InMemory) specifically so the `Unit.PropertyId` navigation filter is proven to actually translate to SQL.
- **Unit Type Active/Inactive enforcement** (`pm-properties-units`) — `UnitServiceTests`: creating or retargeting a unit onto an inactive type fails; leaving an already-assigned inactive type untouched on an edit still succeeds (the `unitTypeId` didn't change, so the inactive check is correctly skipped).

## Deliberately out of scope

- **Section validation** (Continue with an invalid section doesn't advance) is enforced by data-annotation `[Required]` attributes on the wizard ViewModels plus `ModelState.IsValid` in `ApplicationsController.Wizard` — MVC model-binding/validation framework behavior, not custom business logic in the service layer. Covering it would mean a `WebApplicationFactory` integration test, which this project intentionally skips (see below).
- **Role/permission boundaries** (a non-PM can't reach `ReviewController`) are enforced declaratively via `[Authorize(Roles = ...)]` — this is ASP.NET Core's own tested behavior, not project logic worth re-verifying with a unit test.
- `pm-bonus-features` item 5 (optimistic concurrency) isn't tested because it isn't implemented — nothing is skipped there.
- No `WebApplicationFactory`/controller-level tests at all: with controllers this thin, everything worth asserting is already reachable — and asserted — at the service layer.

## Steps (all done)

1. ~~`dotnet new xunit -o PropertyManagement.Tests`, add to the `.slnx`, add project references~~ — done; only an `Infrastructure` reference was needed (Domain/Application come transitively).
2. ~~Test service-layer methods directly~~ — done, no controller tests (see Deliberately out of scope).
3. ~~EF Core Sqlite in-memory for real SQL translation~~ — done via `TestDbContextFactory`.
4. ~~Explicit, deterministic fixtures, no Bogus~~ — done via `TestDataBuilder`.

## Acceptance checks

- ✅ `dotnet test` from the repo root runs the whole suite with no manual setup: 34/34 passing, no live SQL Server/LocalDB dependency (Sqlite in-memory only).
- ✅ Every covered rule above has at least one passing test and one test proving the rejection path, not just the happy path.

## Related skills

`pm-project-setup` (project referencing pattern), `pm-properties-units`, `pm-application-wizard`, `pm-review-list`, `pm-identity-roles` (the business logic under test lives in these).
