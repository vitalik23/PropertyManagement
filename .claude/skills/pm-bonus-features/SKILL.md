---
name: pm-bonus-features
description: The five optional bonus items from the assessment — reusable paged/sorted grid view component with a JSON+OpenAPI endpoint, review queue claim/release, property-manager-only notes, save-with-errors per section, and multi-applicant optimistic concurrency. Use only after the required functionality is complete and working; pick items independently.
---

# Bonus Features (Optional)

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

- Item 1: the JSON endpoint's response total matches the filtered count independent of page size; OpenAPI doc lists the endpoint with its parameters.
- Item 2: a claimed application is not reviewable by a different PM until released; releasing makes it claimable again.
- Item 3: fetching an application's data as an applicant (via any code path — view, JSON endpoint) never includes note content, verified by checking the actual response, not just the rendered HTML.
- Item 4: a section with a blank required field still saves; the Summary lists that specific missing field; Submit is rejected while it's listed.
- Item 5: two concurrent edits to two different sections of the same application both save; two concurrent edits to the *same* section — the second save (by version) is rejected with a reload-prompting message, and the first save's data is not lost.

## Related skills

`pm-review-list`, `pm-application-wizard`, `pm-properties-units`, `pm-identity-roles` — this skill only layers on top of those; read the relevant one first for the item you're picking up.
