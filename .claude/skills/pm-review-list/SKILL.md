---
name: pm-review-list
description: Property manager review workflow (Approve/Return/Deny modal, lease issuance, status/review history) and the application list with database-side filtering by status and property, scoped to own applications for applicants vs all for property managers. Use when building the review modal, lease approval logic, or the applications list page.
---

# Review Workflow & Application List

## Goal

Let a Property Manager open a submitted application, review it through a modal with an outcome (Approve/Return/Deny) plus a comment, have Approve issue a 12-month lease while blocking a second lease on an already-leased unit, and give both roles a properly-scoped, database-filtered list of applications.

## Where this lives

- `PropertyManagement.Domain/` — `ApplicationStatusHistory` (ApplicationId, FromStatus, ToStatus, ChangedByUserId, ChangedAtUtc, Comment), extend `Lease` from `pm-properties-units` if not already present.
- `PropertyManagement.Application/` — `IApplicationReviewService.ReviewAsync(applicationId, outcome, comment, reviewerId)` encapsulating the status transition + lease creation + history write in one transaction.
- `PropertyManagement/Controllers/ReviewController.cs` (or actions on `ApplicationsController`) — `GET Review/{id}` returns the review modal partial, `POST Review/{id}` applies the outcome.
- `PropertyManagement/Controllers/ApplicationsController.cs` — `Index` action for the list, taking `status` and `propertyId` filter parameters.
- `PropertyManagement/Views/Applications/Index.cshtml` + `_ApplicationListPartial.cshtml` (so filter changes can refresh just the table), `Views/Applications/_ReviewModalPartial.cshtml`, `_StatusHistoryPartial.cshtml`.

## Requirements (from spec)

- PM opens a submitted application and completes review via a modal: outcome Approve / Return / Deny, **comment required for Return and Deny**.
- Statuses: Draft, Submitted, Returned, Approved, Denied, Withdrawn. **Approved, Denied, Withdrawn are terminal** (no further transitions).
- The application page shows PMs a **history of status changes and review outcomes** (who, when, comment).
- List filtered by status and property, **filtering done in the database** (`IQueryable` + `Where` translated to SQL — not `ToList()` then LINQ-to-objects). Applicants see only their own applications; PMs see all.
- Approval creates a lease for the unit: start date + 12-month term. A unit whose lease term covers today is not available (rule owned by `pm-properties-units`, enforced here at approval time too).
- At approval, reject the action with an error if the unit already has an active lease (mirrors the submit-time check in `pm-application-wizard`) — the approval check is specifically what prevents a second concurrent lease on the same unit.

## Steps

1. `ReviewOutcome` enum: Approve, Return, Deny. `ReviewViewModel`: Outcome (required), Comment (required only for Return/Deny — enforce with a custom validation attribute or manual `ModelState` check in the action, since `[RequiredIf]` isn't built in).
2. `GET Review/{id}` — 404/403 if not Submitted (only submitted applications are reviewable) or if the current user isn't a PM; return `_ReviewModalPartial` with applicant info, unit, and the application's read-only sections rendered via the same section partials from `pm-application-wizard` (in their read-only mode) so the PM sees exactly what was submitted.
3. `POST Review/{id}` — validate; on failure, **re-render the same partial** with errors (same modal pattern as everywhere else). On success, inside a transaction:
   - Return: `Status = Returned`, write history row with comment. Applicant can now edit again per `pm-application-wizard`'s Draft/Returned rule.
   - Deny: `Status = Denied` (terminal), write history row with comment.
   - Approve: re-check `IUnitAvailabilityService.IsAvailable(unitId, today)` — if false, fail validation with a clear error ("unit already has an active lease") and re-render the modal instead of approving. If available: create `Lease { UnitId, StartDate = today (or a chosen date), EndDate = StartDate.AddMonths(12) }`, `Status = Approved` (terminal), write history row.
   - Close modal + refresh the application detail/list on success, per the standard modal pattern.
4. Status history display: a simple ordered table (`_StatusHistoryPartial`) of `ApplicationStatusHistory` rows, visible only to PMs (applicants don't need the internal review trail per the spec's wording — it says "shows property managers a history," not applicants; keep it PM-only for consistency with the permission model).
5. Application list query — build it as a composable `IQueryable<RentalApplication>`:
   ```csharp
   var query = db.RentalApplications.AsQueryable();
   if (!User.IsInRole(Roles.PropertyManager))
       query = query.Where(a => a.ApplicantUserId == currentUserId); // or Any(x => x.ApplicantUserId == ...) once bonus 5's multi-applicant model exists
   if (status.HasValue) query = query.Where(a => a.Status == status);
   if (propertyId.HasValue) query = query.Where(a => a.Unit.PropertyId == propertyId);
   var results = await query.OrderByDescending(a => a.SubmittedAtUtc).ToListAsync();
   ```
   The `.Where` calls must stay before any `ToList()`/enumeration so SQL Server does the filtering — this is exactly the "done in the database, not in memory" requirement.
6. Filter UI: dropdowns for status and property above the list, submitting via a GET form (or AJAX) that re-renders `_ApplicationListPartial` so filtering doesn't reload the whole page.

## Acceptance checks

- Approving an application for a unit that already has a covering lease is rejected with a clear error and no state change.
- Returning or Denying without a comment is rejected by validation.
- An applicant's list view never shows another applicant's application, even via crafted query-string filters.
- Filtering by status/property produces a query whose generated SQL includes a `WHERE` clause (verify via EF Core logging or `ToQueryString()`) rather than filtering an already-materialized list.
- Status history for an approved/returned/denied application shows every transition with who/when/comment.

## Related skills

`pm-properties-units` (`IUnitAvailabilityService`, Lease entity), `pm-application-wizard` (status history table, section partials reused read-only), `pm-bonus-features` item 1 (paging/sorting extension of this list) and item 2 (review queue claim/release extends this review flow).
