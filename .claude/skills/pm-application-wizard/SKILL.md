---
name: pm-application-wizard
description: The single-page, multi-section rental application wizard — one view model, one form, one action, Continue/Back/Submit dispatched by button name, section partials/view components, Draft/Returned editable vs read-only, Residence History modal CRUD, and the submit-time active-lease check. Use when building the applicant-facing rental application form.
---

# Rental Application Wizard

## Goal

Build the applicant's rental application as **one page** that shows one section at a time (Applicant Information → Residence History → read-only Summary), driven by a single view model and a single form posting to a single action, where the clicked button decides the behavior.

## Where this lives

- `PropertyManagement.Domain/` — `RentalApplication` (Status enum, UnitId, timestamps), `Residence` (child collection: address, landlord name/phone, move-in/move-out dates), applicant info fields (either on `RentalApplication` directly, or a small owned type — keep it simple, one table is fine).
- `PropertyManagement.Application/` — `RentalApplicationViewModel` (drives the whole wizard: current section indicator, applicant-info fields, residence list, validation state per section), section-specific sub-view-models if that keeps validation attributes clean.
- `PropertyManagement/Controllers/ApplicationsController.cs` — one action (e.g. `[HttpPost] Wizard(RentalApplicationViewModel vm, string action)`) handling all three buttons via the `action`/button-`name` value.
- `PropertyManagement/Views/Applications/Wizard.cshtml` — shell page; `Views/Applications/Sections/_ApplicantInfo.cshtml`, `_ResidenceHistory.cshtml` (or a `ResidenceHistoryViewComponent` — the spec requires at least one view component somewhere in the app, and the residence list with its own add/edit/remove modal logic is a strong candidate), `_Summary.cshtml`.
- `PropertyManagement/Views/Applications/_ResidenceFormPartial.cshtml` — the add/edit/remove modal body for a single residence entry.

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

## Steps

1. Model the wizard state as an enum or int (`CurrentSection`: ApplicantInfo = 0, ResidenceHistory = 1, Summary = 2) carried in the view model via a hidden field, not in session/TempData (a hidden field survives validation re-renders cleanly and keeps the wizard stateless per request).
2. `ApplicationsController.Wizard` POST signature takes the full view model plus a `string submitAction` bound from three submit buttons sharing `name="submitAction"` with values `"continue"`, `"back"`, `"submit"`.
3. On `"continue"`: validate only the fields belonging to `CurrentSection` (use `ModelState` prefix filtering, or split into per-section sub-models with their own `[Required]`/etc. attributes so `TryValidateModel` can target just that sub-object) — persist that section's data via `SaveChangesAsync`, then increment `CurrentSection` and re-render the appropriate section partial. On validation failure: don't touch `CurrentSection`, don't save, re-render the same section with `ModelState` errors.
4. On `"back"`: decrement `CurrentSection`, re-render — no DB write, no validation.
5. On `"submit"` (only reachable from Summary): re-check both sections are marked saved (track a bool/timestamp per section, or just re-validate both against the persisted entity), re-check unit availability (`IUnitAvailabilityService` from `pm-properties-units` — reject with a clear error if the unit now has an active lease), then set `Status = Submitted`, write an `ApplicationStatusHistory` row, save.
6. Editable-vs-read-only: a single server-side check (`application.Status is Draft or Returned`) passed into the section partial/view model as a bool; the partial conditionally renders inputs vs plain text based on it. The controller POST action independently re-checks this before persisting **any** section — never trust that the UI honored the read-only flag.
7. Residence History section: list of saved residences + "Add Residence" button opening the modal partial (`_ResidenceFormPartial`); edit/remove per row open the same partial pre-filled or a confirm-then-delete action. This is a good place for the required view component: a `ResidenceListViewComponent` that renders the current list and is invoked from the section partial, re-invoked after each modal save to refresh just that list.
8. Summary section: read-only render of both sections' saved data (reuse the same section partials in read-only mode rather than duplicating markup) plus the Submit button, disabled/absent if either section hasn't been saved yet.
9. Withdraw: a separate simple action (not part of the wizard's Continue/Back/Submit) available on the application's own page while it's in a non-terminal, applicant-actionable status — sets `Status = Withdrawn`, writes history, no lease implications since Withdrawn never had one.

## Acceptance checks

- Submitting Applicant Info with a missing required field re-renders that section with validation messages and does not advance or save.
- Clicking Back never triggers validation and never saves, even if the current section has invalid data typed into it.
- Submit is unreachable/rejected if either section was never successfully saved.
- Editing an already-Submitted application (e.g. via direct POST replay) is rejected by the controller even though the UI wouldn't normally offer the form.
- Submitting an application for a unit that now has an active lease is rejected with an error; a different application for the same unit remains untouched (not auto-withdrawn).

## Related skills

`pm-identity-roles` (ownership check on every action), `pm-properties-units` (`IUnitAvailabilityService`, modal pattern reused for residences), `pm-review-list` (status history table shared with review), `pm-bonus-features` items 4 and 5 (save-with-errors, multi-applicant concurrency) extend this skill's model without changing its core shape.
