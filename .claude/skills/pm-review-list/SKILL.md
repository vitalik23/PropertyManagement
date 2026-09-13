---
name: pm-review-list
description: Property manager review workflow (Approve/Return/Deny modal, lease issuance, status/review history) and the application list with database-side filtering by status and property, scoped to own applications for applicants vs all for property managers. Use when building the review modal, lease approval logic, or the applications list page.
---

# Review Workflow & Application List

## Status: done and verified live

Implemented as `IApplicationReviewService`/`ApplicationReviewService` (Infrastructure) + `ReviewController` (`[Authorize(Roles = Roles.PropertyManager)]`) + extensions to `IApplicationService` (`GetFilteredApplicationsAsync`, `GetStatusHistoryAsync`). All five acceptance checks below verified live via `curl`/`sqlcmd` against a real multi-application, multi-outcome scenario (Return without/with comment, Deny, Approve → real `Lease`, a second Submit blocked by that lease, and the Approve-time check re-verified in isolation by manually forcing a second application to `Submitted` on the now-leased unit). One incidental discovery: a real user's own in-progress application (`test@gmail.com`, created through the browser, not a seed) showed up correctly scoped/protected during testing — ownership and role checks held up against genuine, not just synthetic, data.

**Superseded by `pm-bonus-features` item 1**: the plain `<form method="get">` filter + server-rendered `Review/Index.cshtml`/`Applications/Index.cshtml` tables described in Steps 5–6 below were replaced by a single reusable `ApplicationsGridViewComponent` (paged, sortable, backed by the `GET /api/applications` JSON endpoint) used on both pages. `ApplicationsController.ListPartial` and `_ApplicationsListPartial.cshtml` were removed as dead code once the grid took over. `IApplicationService.GetFilteredApplicationsAsync` (unpaged) is unchanged and still used directly by existing tests; the grid calls a new sibling, `GetFilteredApplicationsPageAsync`. See `pm-bonus-features` for the full design and the "why manual concurrency, not EF's" reasoning that applies to a different bonus item but lives in the same file.

## Goal

Let a Property Manager open a submitted application, review it through a modal with an outcome (Approve/Return/Deny) plus a comment, have Approve issue a 12-month lease while blocking a second lease on an already-leased unit, and give both roles a properly-scoped, database-filtered list of applications.

## Where this lives

- `PropertyManagement.Domain/Entities/ApplicationStatusHistory.cs`, `Lease.cs` — already existed from `pm-project-setup`, unchanged here.
- `PropertyManagement.Infrastructure/Services/IApplicationReviewService.cs` (+ `enum ReviewOutcome { Approve, Return, Deny }`) / `ApplicationReviewService.cs` — `ReviewAsync(applicationId, outcome, comment, reviewerId)`. Loads the application fresh, rejects if `Status != Submitted`, rejects Return/Deny without a comment (defense-in-depth — the controller checks too), calls `IUnitAvailabilityService.IsAvailableAsync` before Approve creates the `Lease` (`StartDate = today`, `EndDate = today.AddMonths(12)`), always writes an `ApplicationStatusHistory` row. Returns the existing `ApplicationActionResult` from `IApplicationService.cs` — no new result type.
- `PropertyManagement.Infrastructure/Services/IApplicationService.cs`/`ApplicationService.cs` extended with `GetFilteredApplicationsAsync(Guid? applicantUserId, ApplicationStatus? status, Guid? propertyId)` (one composable `IQueryable`, `.Where`s added before `ToListAsync()` — verified via EF SQL logging that `status`/`propertyId` land in the generated `WHERE` clause, not an in-memory filter) and `GetStatusHistoryAsync(Guid applicationId)`. `GetByIdAsync` now also `.Include(a => a.ApplicantUser)`.
- `PropertyManagement/Controllers/ReviewController.cs` (`[Authorize(Roles = Roles.PropertyManager)]`) — `Index(status?, propertyId?)` (the full list, `applicantUserId: null` so it sees everyone), `Details(id)`/`DetailsPartial(id)` (read-only view + history, the latter is also the modal's `data-refresh-url` target), `Review(id)` GET/POST (the outcome modal).
- `PropertyManagement/Controllers/ApplicationsController.cs` — `Index`/`ListPartial` now take the same `status`/`propertyId` filters and call the same `GetFilteredApplicationsAsync` (with `applicantUserId: CurrentUserId`) — one shared, DB-filtered method serves both roles' lists, exactly as the spec's "Applicants see their own, PM sees all" implies a single feature, not two.
- `PropertyManagement/Views/Review/Index.cshtml` (list + GET filter form — plain page reload, not AJAX, per the spec's "GET form or AJAX" allowance), `Details.cshtml` + `_ApplicationDetailsPartial.cshtml` (reuses `~/Views/Applications/Sections/_SummarySection.cshtml` by absolute path for the read-only both-sections view — that partial already ignores `IsEditable` and always renders read-only, so it's safe to reuse for any status), `_ReviewModalPartial.cshtml`, `_StatusHistoryPartial.cshtml` (PM-only, per the spec's wording — not shown to Applicants). `PropertyManagement/Views/Applications/Index.cshtml` gained the identical filter-form UI for the Applicant side.
- `Models/ReviewViewModels/ReviewViewModel.cs` (`ApplicationId`, `ReviewOutcome Outcome`, `string? Comment`), `ApplicationDetailsViewModel.cs` (wraps a `RentalApplicationWizardViewModel` — reused to drive `_SummarySection.cshtml` — plus `Status`, `ApplicantEmail`, `History`).
- `_PortalLayout.cshtml` — PM sidebar gained "Applications" → `Review/Index`, alongside Properties/Unit Types.

## Requirements (from spec)

- PM opens a submitted application and completes review via a modal: outcome Approve / Return / Deny, **comment required for Return and Deny**.
- Statuses: Draft, Submitted, Returned, Approved, Denied, Withdrawn. **Approved, Denied, Withdrawn are terminal** (no further transitions).
- The application page shows PMs a **history of status changes and review outcomes** (who, when, comment).
- List filtered by status and property, **filtering done in the database** (`IQueryable` + `Where` translated to SQL — not `ToList()` then LINQ-to-objects). Applicants see only their own applications; PMs see all.
- Approval creates a lease for the unit: start date + 12-month term. A unit whose lease term covers today is not available (rule owned by `pm-properties-units`, enforced here at approval time too).
- At approval, reject the action with an error if the unit already has an active lease (mirrors the submit-time check in `pm-application-wizard`) — the approval check is specifically what prevents a second concurrent lease on the same unit.

## Steps (all done)

1. ~~`ReviewOutcome` enum + `ReviewViewModel`~~ — done. Comment-required-for-Return/Deny is a manual `ModelState` check in `ReviewController.Review(POST)` (not a custom attribute) plus the same check again inside `ApplicationReviewService.ReviewAsync` — verified live both ways stay in sync.
2. ~~`GET Review/{id}`~~ — implemented as `ReviewController.Review(Guid id)`, 404 if `Status != Submitted`; the read-only view of the application is on the separate `Details`/`DetailsPartial` actions (reusing `_SummarySection.cshtml` from `pm-application-wizard`), not duplicated into the review modal itself — the modal (`_ReviewModalPartial`) only carries the outcome/comment form, opened from a "Review" button on the Details page.
3. ~~`POST Review/{id}`~~ — done exactly as planned (Return/Deny/Approve branches, `ApplicationStatusHistory` row every time). Refresh-after-success targets `#application-details` (`Review/DetailsPartial/{id}`) — the Details page updates in place to show the new status, lease info, and history without a redirect.
4. ~~Status history display~~ — `_StatusHistoryPartial.cshtml`, PM-only (only reachable via `ReviewController`, never rendered on the Applicant's own wizard/summary view).
5. ~~Application list query~~ — `IApplicationService.GetFilteredApplicationsAsync(applicantUserId, status, propertyId)`, one shared method for both roles (`applicantUserId: null` for PM, `CurrentUserId` for Applicant). Verified via EF SQL logging: the generated query has `WHERE [r].[Status] = @status` (and the property/applicant equivalents) — genuinely DB-side, not `ToList()` then LINQ-to-objects.
6. ~~Filter UI~~ — plain `<form method="get">` with Status (`Enum.GetValues<ApplicationStatus>()`) and Property (`IPropertyService.GetAllAsync()`) dropdowns, reloading the page with query params. Chose the GET-form option the spec explicitly allows over AJAX — no extra JS needed, and filtering isn't part of the modal contract.

## Acceptance checks

- ✅ Approving an application for a unit that already has a covering lease is rejected with a clear error and no state change. Verified both the Submit-time block (real `Lease` from a real Approve blocked a second application's Submit) and the Approve-time block in isolation (manually forced a second application to `Submitted` on the now-leased unit — Approve rejected it with "This unit already has an active lease.", no second `Lease` row created).
- ✅ Returning or Denying without a comment is rejected by validation; status unchanged.
- ✅ An applicant's list view never shows another applicant's application — confirmed against a *real* user-created application (not a seed), not just synthetic test data.
- ✅ Filtering by status/property produces a query whose generated SQL includes a `WHERE` clause (confirmed via EF Core console logging) rather than filtering an already-materialized list.
- ✅ Status history for an approved/returned/denied application shows every transition with who/when/comment — verified for a Return (with comment) followed immediately after by re-checking the history table.

## Related skills

`pm-properties-units` (`IUnitAvailabilityService`, Lease entity), `pm-application-wizard` (status history table, section partials reused read-only), `pm-bonus-features` item 1 (paging/sorting extension of this list) and item 2 (review queue claim/release extends this review flow).
