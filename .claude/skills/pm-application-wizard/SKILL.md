---
name: pm-application-wizard
description: The single-page, multi-section rental application wizard — one view model, one form, one action, Continue/Back/Submit dispatched by button name, section partials/view components, Draft/Returned editable vs read-only, Residence History modal CRUD, and the submit-time active-lease check. Use when building the applicant-facing rental application form.
---

# Rental Application Wizard

## Goal

Build the applicant's rental application as **one page** that shows one section at a time (Applicant Information → Residence History → read-only Summary), driven by a single view model and a single form posting to a single action, where the clicked button decides the behavior.

## Status: done and verified live

All of the below is implemented and was verified end-to-end via `curl` (real antiforgery tokens, real cookie sessions) plus direct `sqlcmd` inspection/manipulation for the parts no UI can drive yet (inserting a covering `Lease` — there's no Lease-issuing UI until `pm-review-list` builds Approve).

**Bug found and fixed after initial build:** the user reported Continue "looping back to the start" in the browser. Root cause — `GET Wizard/{id}` unconditionally forced `CurrentSection = ApplicantInfo` whenever the application was editable, regardless of what was already saved. Data was genuinely persisted correctly on each `Continue`, but *any* fresh GET mid-flow (a browser refresh, re-clicking "Continue" from "My Applications", anything that re-renders the page via GET rather than the in-flight POST response) reset the visible section back to step 1 — indistinguishable from "nothing I do advances past step 1" even though the underlying data was fine. **Fix:** the GET handler now computes the resume section from the two persisted timestamps (`ApplicantInfoCompletedAt`/`ResidenceHistoryCompletedAt`) instead of hardcoding `ApplicantInfo` — see `ResumeSection(RentalApplication)` in `ApplicationsController.cs`. Verified live: fill step 1 → simulate a reload (plain `GET Wizard/{id}`, no POST) → resumes at Residence History, not Applicant Info; same for Summary after step 2. A brand-new untouched Draft still correctly starts at Applicant Info. Two defensive changes shipped alongside it, not confirmed as the root cause but real gaps: (1) `Wizard.cshtml` now renders Continue/Submit **before** Back in the DOM (`.wizard-actions`/`.btn-back` CSS `order: -1` keeps Back visually on the left) — with 2+ submit buttons in one form, implicit submission (Enter key, some mobile keyboards) activates the *first* one in DOM order, which was "Back"; (2) the POST action's fallback for an unrecognized `submitAction` now re-renders `model.CurrentSection` instead of silently defaulting to `ApplicantInfo`, so a future mismatch is visibly "stuck" rather than looking like a reset. **Anyone resuming this skill: if you ever add a new place that GETs the Wizard, route it through `ResumeSection`, not a hardcoded section.**

**Extended by `pm-bonus-features` items 4 and 5** (both optional, both done): item 4 changed `ApplicantInfoCompletedAt`/`ResidenceHistoryCompletedAt` from "section is valid" to "section has been visited/attempted at least once" — Continue now persists the section even when invalid (save-with-errors), and `SubmitAsync`'s gate + the Summary's blocking-issues list re-validate live via `IApplicationService.ValidateApplicantInfo`/`ValidateResidenceHistory` instead of trusting those timestamps. Item 5 added `RentalApplication.CoApplicants`, widened every ownership check in this controller (`IsApplicantOnApplication`, replacing bare `ApplicantUserId == CurrentUserId` comparisons) so an invited co-applicant has the same access as the primary applicant, and added manual per-section `int` concurrency versions (`ApplicantInfoVersion`/`ResidenceHistoryVersion` on `RentalApplication`, `Version` on `Residence`) posted as hidden form fields. See `pm-bonus-features` for the full design and why the concurrency tokens are manual `int`s rather than EF's native rowversion.

## Where this lives

- `PropertyManagement.Domain/Entities/RentalApplication.cs` — `Status`, `UnitId`/`Unit`, `ApplicantUserId`/`ApplicantUser`, `FullName`/`PhoneNumber`/`Email`/`CurrentAddress`, plus **`ApplicantInfoCompletedAt`/`ResidenceHistoryCompletedAt`** (`DateTime?`, added specifically for this feature) — explicit per-section "saved" markers, since a section with all-default/empty values (e.g. zero residences) is a legitimate real state, not evidence the section was never visited. `Residence` — unchanged from `pm-project-setup`.
- `PropertyManagement.Infrastructure/Services/IApplicationService.cs`/`ApplicationService.cs` — same style as `PropertyService`/`UnitService`: primitive params, returns `ApplicationActionResult(bool Succeeded, string? Error)`. Every mutating method reloads the entity from DB and re-checks ownership (`ApplicantUserId`) + editability (`Status is Draft or Returned`) itself — the controller never is the only gate. `SubmitAsync` injects `IUnitAvailabilityService` (from `pm-properties-units`) for the active-lease check.
- `PropertyManagement/Controllers/ApplicationsController.cs` (`[Authorize(Roles = Roles.Applicant)]`) — `Start` (find-or-create Draft, no availability gate — ToS only gates Submit/Approve), `Wizard` GET/POST (the one form/one action, `submitAction` = `back`/`continue`/`submit`), `AddResidence`/`EditResidence`/`DeleteResidence` GET/POST (modal contract), `ResidenceList` (AJAX-refresh endpoint for the residence view component), `Index`/`ListPartial` ("My Applications" — minimal, no filters, see note below), `Withdraw` GET/POST (modal contract).
- `PropertyManagement/Views/Applications/Wizard.cshtml` — one `<form>`, `@switch`-style section include based on `CurrentSection`; `Views/Applications/Sections/_ApplicantInfoSection.cshtml`, `_ResidenceHistorySection.cshtml`, `_SummarySection.cshtml`. **`ResidenceListViewComponent`** (`InvokeAsync(Guid applicationId, bool isEditable)`) is the view component for this feature area (same pattern as `PropertiesListViewComponent`/`UnitsListViewComponent`), invoked from both `_ResidenceHistorySection` (editable) and `_SummarySection` (forced read-only).
- `PropertyManagement/Views/Applications/_ResidenceFormPartial.cshtml`/`_ResidenceDeleteConfirmPartial.cshtml`/`_WithdrawConfirmPartial.cshtml` — the modal contract from `pm-properties-units` (`data-modal-url`/`data-modal-form`/`data-refresh-url`/`data-refresh-target`), no new JS needed.
- `PropertyManagement/Views/Applications/Index.cshtml` + `_ApplicationsListPartial.cshtml` — **"My Applications"**, added beyond the original skill scope because without it a Draft/Submitted/Returned application had no way to be reopened after the initial "Apply" redirect. Deliberately minimal (no filtering/paging/sorting) — the full **Application List** with DB-side filtering for both roles is `pm-review-list`'s job; this is Applicant-only and just lists their own rows.
- `Views/Browse/Index.cshtml` — "Apply" is now a real link (`Applications/Start?unitId=...`), no longer disabled.
- `_PortalLayout.cshtml` — sidebar gained "My Applications" for `Roles.Applicant` (alongside "Browse Units").
- Migration `AddApplicationSectionTimestamps` — the two new nullable columns, applied and verified.

## Requirements (from spec)

- Two sections + summary: **Applicant Information** (name, phone, email, current address), **Residence History** (list of prior residences: address, landlord name+phone, move-in/move-out dates), **Summary** (read-only view of both + Submit).
- One view model drives it; each section renders through its own partial view or view component.
- **One form posts to one action**; the clicked button determines behavior:
  - **Continue**: validate the *current* section only, persist it *only if valid*, advance to the next section (or Summary after the last one). Invalid → re-render the current section with errors, don't advance, don't save.
  - **Back**: return to the previous section, **no save**.
  - **Submit**: available from Summary only, only once both sections have been saved.
- Section partial renders **editable or read-only based on a server-side decision**: editable while status is Draft or Returned, read-only otherwise. Controller must reject posts when editing isn't allowed (not just hide the UI).
- Residences added/edited/removed **through a modal** (same modal pattern as `pm-properties-units`: invalid → re-render partial with errors; valid → close + refresh).
- At submit (and again at approval, handled in `pm-review-list`), reject the action with an error if the unit already has an active lease. Other open applications for that same unit are left untouched — this is not a "cancel the others" flow, it only blocks the specific submit/approve action.

## Steps (all done)

1. ~~Wizard state~~ — `WizardSection` enum (`ApplicantInfo=0, ResidenceHistory=1, Summary=2`), carried via a hidden `CurrentSection` field on the form, exactly as planned.
2. ~~POST signature~~ — `Wizard(RentalApplicationWizardViewModel model, string submitAction)`, three buttons sharing `name="submitAction"` (`back`/`continue`/`submit`). `Back` additionally has `formnovalidate` on its `<button>` so jQuery unobtrusive client-side validation doesn't block it — matches "Back never triggers validation" even on the client, not just server.
3. ~~Continue~~ — since only `RentalApplicationWizardViewModel`'s 4 Applicant-Info fields carry `[Required]`, plain `ModelState.IsValid` is already correctly scoped to "the current section" without needing prefix filtering or sub-models (Residence History has no inline fields on this view model at all — it's managed entirely via the separate residence-modal endpoints). Invalid → same section, `CurrentSection` untouched, nothing persisted (verified: posting empty Applicant Info fields re-renders with all 4 validation messages, no DB write).
4. ~~Back~~ — reloads the entity fresh from DB (ignores whatever was posted), decrements `CurrentSection`, no validation, no save (verified live).
5. ~~Submit~~ — `ApplicationService.SubmitAsync` checks both completion timestamps, calls `IUnitAvailabilityService.IsAvailableAsync`, sets `Status = Submitted`, writes `ApplicationStatusHistory`. Verified live both ways: blocked with "This unit is no longer available." while a covering `Lease` existed (status stayed Draft), succeeded once the test `Lease` was removed.
6. ~~Editable-vs-read-only~~ — `IsEditable` computed server-side (`Status is Draft or Returned`) on every request, never trusted from the client; verified a POST attempting to change `FullName` on an already-`Submitted` application is silently ignored (forced back to read-only Summary, DB value unchanged).
7. ~~Residence History~~ — `ResidenceListViewComponent` + modal CRUD (`AddResidence`/`EditResidence`/`DeleteResidence`), refreshed via `Applications/ResidenceList?applicationId=...` after each modal save — verified live (add → appears immediately in the list).
8. ~~Summary~~ — reuses the same read-only rendering, plus the Submit button (only rendered when `IsEditable && ApplicantInfoSaved && ResidenceHistorySaved`).
9. ~~Withdraw~~ — separate modal-driven action, `Status = Withdrawn`, history row; verified live (Submitted → Withdrawn).

## Acceptance checks

- ✅ Submitting Applicant Info with a missing required field re-renders that section with validation messages and does not advance or save.
- ✅ Clicking Back never triggers validation and never saves, even if the current section has invalid data typed into it.
- ✅ Submit is unreachable/rejected if either section was never successfully saved (enforced via the two completion timestamps, not derived field-emptiness checks).
- ✅ Editing an already-Submitted application (e.g. via direct POST replay) is rejected by the controller even though the UI wouldn't normally offer the form.
- ✅ Submitting an application for a unit that now has an active lease is rejected with an error; a different application for the same unit remains untouched (not auto-withdrawn).
- ✅ Ownership: a different Applicant opening someone else's application by guessing its id gets 404. A PropertyManager hitting any `ApplicationsController` action gets redirected to `/Account/AccessDenied`.

## Related skills

`pm-identity-roles` (ownership check on every action), `pm-properties-units` (`IUnitAvailabilityService`, modal pattern reused for residences), `pm-review-list` (status history table shared with review), `pm-bonus-features` items 4 and 5 (save-with-errors, multi-applicant concurrency) extend this skill's model without changing its core shape.
