---
name: pm-properties-units
description: Property and Unit management — modal-driven CRUD populated from partial views, the Unit Type Active/Inactive lookup rule enforced server-side, unit availability based on lease dates, and the applicant browse view. Use when building property/unit management or the unit browse/listing page.
---

# Properties & Units

## Goal

Let a Property Manager maintain properties and units through modals (add/edit/remove), enforce the Unit Type lookup's Active/Inactive rule on the server, and let an Applicant browse only units that are actually available today.

## Where this lives

- `PropertyManagement.Domain/` — `Property`, `Unit` (UnitNumber, Bedrooms, MonthlyRent, `UnitTypeId`), `UnitType` (Name, `IsActive`), `Lease` (UnitId, StartDate, EndDate/TermMonths).
- `PropertyManagement.Application/` (or Infrastructure services) — `IUnitAvailabilityService`/similar encapsulating "is this unit available today" (a unit is unavailable if any Lease row's date range covers today — this logic is reused at browse time, at application-submit time, and at approval time, so put it in one place, not three).
- `PropertyManagement/Controllers/PropertiesController.cs`, `UnitsController.cs` (or nest units under properties — your call, but keep it consistent with how units are routed from application/unit browsing).
- `PropertyManagement/Views/Properties/` — Index (list) + `_PropertyFormPartial.cshtml`, `_UnitFormPartial.cshtml` (modal bodies).
- `PropertyManagement/ViewComponents/` — a view component is required somewhere in the app; the available-units browse list (or the unit availability badge) is a natural fit since it's reused and has its own query logic.

## Requirements (from spec)

- PM can add/edit/remove properties and units (unit number, bedrooms, monthly rent, unit type) **through modals**.
- Applicants browse available units and start an application for one.
- Unit Type is a lookup with Active/Inactive values. An inactive value still displays on a unit already using it but **cannot be selected for any other unit — enforced on the server**, not just hidden client-side.
- A unit whose lease term covers today is not available.

## Modal pattern (required — applies here and in every other modal in the app)

1. A button/link triggers a client-side fetch (or plain `<a>` + Bootstrap modal JS) to a controller action that returns a **partial view** — this fills the modal body.
2. The partial's form posts back to a controller action.
3. **Validation fails** → the action re-renders and returns the **same partial** with `ModelState` errors; the client swaps it back into the modal body so the modal stays open showing the errors (no full page reload, no redirect on failure).
4. **Validation succeeds** → the action performs the save and returns something the client uses to close the modal and refresh only the affected page region (e.g. a redirect the client follows and then closes the modal, or a small JSON success signal + an AJAX refresh of the list partial). Pick one mechanism and apply it consistently across every modal in the app (properties/units, residence history, review).

## Steps

1. `UnitTypesController` (or fold into a lookups admin section) — Active/Inactive is itself a small CRUD; simplest correct approach is a boolean toggle, not delete (you cannot delete a `UnitType` already referenced by a unit without breaking history).
2. Unit create/edit partial: dropdown of `UnitType` sourced as **Active types, plus the unit's current type if it's Inactive** (so editing an already-inactive-typed unit doesn't silently blank the field or force a change).
3. Server-side enforcement: in the POST action, if the posted `UnitTypeId` is Inactive **and** it's not already the unit's current value (i.e., new unit, or an edit that's *changing* to a different type), add a `ModelState` error and re-render the partial. Client-side hiding of inactive options in the dropdown is a UX nicety, not a substitute — a manipulated request must still be rejected server-side.
4. Property delete: block/confirm if the property has units with active leases or non-terminal applications, or cascade per your judgment — document the choice.
5. Availability: `IUnitAvailabilityService.IsAvailable(unitId, DateTime today)` — a unit is unavailable if `Leases.Any(l => l.UnitId == unitId && l.StartDate <= today && l.EndDate >= today)`. Use this everywhere availability is checked (browse list, application start, submit-time re-check, approval-time re-check).
6. Applicant browse view: list/grid of units where `IsAvailable == true`, showing property, unit number, bedrooms, rent, unit type; "Apply" starts a new application only from here.

## Acceptance checks

- Editing a unit whose current type is Inactive still shows that type selected; changing it to a different Inactive type is rejected server-side even if the dropdown were manipulated to offer it.
- A unit with a lease covering today does not appear in the applicant browse list.
- Modal validation failures re-render the same modal with errors and no page navigation; successful saves close the modal and the underlying list updates without a full reload.

## Related skills

`pm-overview`, `pm-project-setup` (UnitType lookup seeding), `pm-application-wizard` (consumes availability at submit), `pm-review-list` (consumes availability at approval, creates the Lease).
