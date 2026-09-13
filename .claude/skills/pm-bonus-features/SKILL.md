---
name: pm-bonus-features
description: The five optional bonus items from the assessment — reusable paged/sorted grid view component with a JSON+OpenAPI endpoint, review queue claim/release, property-manager-only notes, save-with-errors per section, and multi-applicant optimistic concurrency. Use only after the required functionality is complete and working; pick items independently.
---

# Bonus Features (Optional)

## Status: items 1, 4, 5 done and verified live; items 2 and 3 not implemented (user's choice)

- **Item 1 (grid)**: `PropertyManagement.Infrastructure/Services/PagedResult.cs`, `IApplicationService.GetFilteredApplicationsPageAsync` (filters shared with the unpaged method via a new private `BuildFilteredQuery`, `OrderBy`/`Skip`/`Take` all before `ToListAsync()`), `PropertyManagement/Controllers/Api/ApplicationsApiController.cs` (`[ApiController] [Route("api/applications")]`, role-scoped exactly like `ApplicationsController`/`ReviewController`), `ApplicationsGridViewComponent` + `Views/Shared/Components/ApplicationsGrid/Default.cshtml`, `wwwroot/js/applications-grid.js` (jQuery `$.getJSON` — deliberately matching the existing `modal-forms.js` AJAX idiom rather than introducing `fetch()`). The same component now drives both `Applications/Index` (Applicant) and `Review/Index` (PM) — one grid, two pages, exactly as the bonus asks. `Program.cs` has `AddOpenApi()`/`MapOpenApi()` (.NET 10's built-in generator, `Microsoft.AspNetCore.OpenApi` package — no Swashbuckle needed); doc is served at `/openapi/v1.json`. `ApplicationsController.ListPartial` and `_ApplicationsListPartial.cshtml` were removed as dead code once the grid replaced them.
- **Item 4 (save-with-errors)**: `PropertyManagement.Domain/Validation/ApplicantInfoValidationTarget.cs` defines the Applicant Information rules once; `RentalApplicationWizardViewModel : ApplicantInfoValidationTarget` inherits them (no `asp-for` changes needed in the views). `IApplicationService.ValidateApplicantInfo`/`ValidateResidenceHistory` re-validate persisted data (not just a live postback) via `Validator.TryValidateObject` — used both to build `RentalApplicationWizardViewModel.BlockingIssues` (rendered in `_SummarySection.cshtml`) and to gate `SubmitAsync` (replaced the old `ApplicantInfoCompletedAt is null` check, since that flag now only means "visited," not "valid"). `ApplicationsController.Wizard` POST calls `SaveApplicantInfoAsync` unconditionally before checking `ModelState.IsValid`, so invalid input is persisted and survives a reload.
- **Item 5 (multi-applicant + concurrency)**: `ApplicationApplicant` join entity + `RentalApplication.CoApplicants`; ownership centralized in `IsEditableOwned`/`GetEditableOwnedAsync` (`ApplicantUserId == userId || CoApplicants.Any(...)`), which every mutating method already funnels through. **Concurrency is deliberately manual `int` columns** (`RentalApplication.ApplicantInfoVersion`/`ResidenceHistoryVersion`, `Residence.Version`), not EF `IsConcurrencyToken()`/`[Timestamp]` — see the "why" note below, this is the one place this skill's original draft (below) turned out to be wrong. `AddCoApplicantAsync` (looks up by email via `UserManager<User>`, requires the Applicant role) + an "Invite" modal on the wizard page, refreshed via `_CoApplicantsListPartial.cshtml`.

**Why manual `int` versions instead of EF's native rowversion (corrects this skill's original plan below):** EF Core's optimistic concurrency check adds *every* `IsConcurrencyToken()` property on an entity to the `WHERE` clause of *any* update to that entity — not just the columns actually being changed. Since Applicant Info and Residence History both live as columns on the same `RentalApplication` row, two native EF concurrency tokens on that one entity would make a save to one section spuriously fail if the *other* section had been saved concurrently — the opposite of the spec's "saves to different sections must not interfere." Manually comparing and incrementing a plain `int` per section, entirely in application code, keeps the two checks genuinely independent. It also sidesteps a real portability problem: SQL Server's `rowversion` type (what `[Timestamp]` maps to) has no equivalent in Sqlite, which `PropertyManagement.Tests` uses — a native rowversion column would behave differently (or not at all) under the test suite than in production.

Verified live end-to-end with two real logged-in sessions (primary + invited co-applicant) against a disposable throwaway LocalDB: co-applicant successfully saved Applicant Info (version 0→1); the primary applicant's stale concurrent save (still holding version 0) was rejected with "This section was changed by another applicant. Reload to see the latest version." and did not overwrite the co-applicant's data; the primary's *unrelated* Residence History confirm (version 0→1) succeeded immediately afterward, proving the two sections don't interfere; a genuine stranger (not invited) still got 404 on the same application.

## Goal

Each item below is scored independently and none is required for a complete, passing submission — implement whichever you have time for, in any order, without needing the others. Do this only after `pm-overview`'s core requirements checklist is fully done.

## Where this lives

Extends the projects/files established in `pm-review-list`, `pm-application-wizard`, and `pm-properties-units` — no new project layout, just additional entities/actions/components layered on.

## 1. Paging/sorting grid as a reusable view component + JSON endpoint + OpenAPI

- Extend the `pm-review-list` list query with `OrderBy`/`Skip`/`Take`, all still database-side.
- `GridController` (or an action on `ApplicationsController`) exposing `GET /api/applications?status=&propertyId=&page=&pageSize=&sortBy=&sortDir=` returning `{ rows: [...], totalCount: n }` as JSON.
- Add Swagger/OpenAPI (`Microsoft.AspNetCore.OpenApi` or Swashbuckle) and annotate the endpoint so it shows up documented.
- Build a `GridViewComponent` (Razor) that renders the table/pager chrome and fetches rows from that JSON endpoint client-side (fetch/AJAX) — reusable if you also want it for the property/unit lists.

## 2. Review queue (claim / release)

- Add `Status = UnderReview` conceptually as a claim, not a new terminal/lifecycle status — model it as `RentalApplication.ClaimedByUserId` + `ClaimedAtUtc` nullable fields rather than overloading the status enum from `pm-review-list` (keeps the required six-status model untouched).
- "Claim": PM action on a Submitted, unclaimed application sets `ClaimedByUserId = currentUserId`; the review modal from `pm-review-list` becomes available only to the claimant (or any PM if you'd rather keep it simple — decide and document).
- "Release": clears the claim fields, application returns to the general queue.
- List view (`pm-review-list`) shows claim state per row and who holds it.

## 3. Property-manager-only notes

- `PropertyManagerNote` entity (or a nullable text column on `RentalApplication` if you want it simpler) — free-text, editable by any PM.
- Rendered **only** in PM-facing views (review modal, application detail when viewed by a PM) — audit every view model / partial that an applicant's request path can reach and make sure the notes field is never included in the data sent to an applicant-rendered view, not just hidden by CSS. If applications are ever exposed via the JSON endpoint from item 1, exclude notes from the applicant-scoped response shape entirely (don't just filter it in JS).

## 4. Save-with-errors per section + Summary blocking list

- Change `pm-application-wizard`'s Continue behavior: persist the section even when invalid (store the entity as-is), but keep the validation errors attached and displayed.
- Track outstanding errors per section (e.g. a computed property re-running the section's validation rules against the persisted data, rather than only checking at Continue-time) so the Summary can list every unresolved issue across both sections.
- Define each section's validation rules **once** (a `FluentValidation` validator per section, or a single method returning `IEnumerable<ValidationResult>` mapped to `ModelState` keys) and reuse that same rule set both at Continue-time (immediate feedback) and at Summary-time (blocking list) — don't duplicate the rules in two places.
- Submit stays blocked (disabled button, and the controller rejects the POST) while the shared validator returns any error for either section.

## 5. Multiple applicants per application, with per-section optimistic concurrency

- Join table `ApplicationApplicant(RentalApplicationId, UserId)` — any listed user can view/edit; every ownership check in `pm-application-wizard`, `pm-identity-roles`, and `pm-review-list`'s list scoping changes from `ApplicantUserId == currentUserId` to `Applicants.Any(a => a.UserId == currentUserId)`.
- Add a concurrency token (EF Core `[Timestamp]`/`RowVersion` column, or a manual `int Version` incremented on save) **per section**, not one global token on the whole application — two applicants saving different sections concurrently must both succeed.
- On a section save (`pm-application-wizard`'s Continue action), pass the version the form was loaded with; if it doesn't match the current DB value, reject with `DbUpdateConcurrencyException` (EF's built-in mechanism does this for free with `RowVersion`) and show "this section was changed by another applicant — reload to see the latest version" instead of overwriting.
- No real-time sync needed — this is purely a save-time check, not a live-collaboration feature.

## Acceptance checks (per item, only for items you implement)

- ✅ Item 1: the JSON endpoint's response total matches the filtered count independent of page size (verified: `pageSize=2` → 2 rows, `totalCount=6`; `pageSize=100` → same `totalCount=6`); `/openapi/v1.json` lists `paths."/api/applications"`.
- Item 2: not implemented.
- Item 3: not implemented.
- ✅ Item 4: a section with a blank/invalid required field still saves (verified: an invalid email was persisted to the DB); the Summary lists that specific missing/invalid field; Submit is rejected server-side even when forged directly, while it's listed.
- ✅ Item 5: two concurrent edits to two different sections of the same application both save (verified live); two concurrent edits to the *same* section — the second save is rejected with a reload-prompting message, and the first save's data is not lost (verified live, real DB state checked after each step).

## Related skills

`pm-review-list`, `pm-application-wizard`, `pm-properties-units`, `pm-identity-roles` — this skill only layers on top of those; read the relevant one first for the item you're picking up.
