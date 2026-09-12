---
name: pm-properties-units
description: Property and Unit management — modal-driven CRUD populated from partial views, the Unit Type Active/Inactive lookup rule enforced server-side, unit availability based on lease dates, and the applicant browse view. Use when building property/unit management or the unit browse/listing page.
---

# Properties & Units

## Goal

Let a Property Manager maintain properties and units through modals (add/edit/remove), enforce the Unit Type lookup's Active/Inactive rule on the server, and let an Applicant browse only units that are actually available today.

## Where this lives

- `PropertyManagement.Domain/` — `Property`, `Unit` (UnitNumber, Bedrooms, MonthlyRent, `UnitTypeId`), `UnitType` (Name, `IsActive`), `Lease` (UnitId, StartDate, EndDate) — all already exist (see `pm-project-setup`).
- `PropertyManagement.Infrastructure/Services/` — `IUnitAvailabilityService`/similar encapsulating "is this unit available today" (a unit is unavailable if any Lease row's date range covers today — this logic is reused at browse time, at application-submit time, and at approval time, so put it in one place, not three). **Not built yet** — needed before the applicant browse view or the wizard's submit-time check.
- `PropertyManagement.Infrastructure/Services/IPropertyService.cs`/`PropertyService.cs` — **done**, primitive-param CRUD (`GetAllAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`), same style as `AuthService`/`RoleSeeder` (no Web-layer types leak into Infrastructure).
- `PropertyManagement/Controllers/PropertiesController.cs` — **done**, `[Authorize(Roles = Roles.PropertyManager)]`. `UnitsController.cs` — not built yet (nest under `/Properties/{propertyId}/Units` when it's picked up, for URL consistency with "a unit belongs to a property").
- `PropertyManagement/Views/Properties/` — **done**: `Index.cshtml`, `_PropertyFormPartial.cshtml` (shared Create/Edit, `Model.Id.HasValue` picks the action), `_PropertyDeleteConfirmPartial.cshtml`. `_UnitFormPartial.cshtml` not built yet. `Index.cshtml` now sets `Layout = "_PortalLayout"` (see below) instead of the default site layout — Properties is reachable only through the Portal now, not the top nav.
- **Portal shell (done, not originally in this skill but everything PM/Applicant-facing now lives behind it):** `Views/Shared/_PortalLayout.cshtml` (nested layout — `Layout = "_Layout"`, renders a role-aware sidebar + `@RenderBody()`), `PortalController` (`Index`, `Profile` — `[Authorize]`, any role), `Views/Portal/Index.cshtml`/`Profile.cshtml`. The top header (`_Layout.cshtml`) shows a user-icon dropdown (hover-opened via CSS, `Views/Shared/_Layout.cshtml` + `.navbar .dropdown:hover` rule in `site.css`) linking to `/Portal` when authenticated, instead of the old inline email+Logout. **Any future PM/Applicant-facing page (`UnitsController`, applicant browse, application wizard, review) should set `Layout = "_PortalLayout"` and add its sidebar link in `_PortalLayout.cshtml`'s role-conditional list, following the `Проперті` pattern.**
- `PropertyManagement/ViewComponents/PropertiesListViewComponent.cs` + `Views/Shared/Components/PropertiesList/Default.cshtml` — **done**, this is the view component the spec requires somewhere in the app (satisfied here; the applicant browse list can either reuse this component or get its own — decide when building it).
- `PropertyManagement/Models/PropertyViewModels/` — **done**: `PropertyFormViewModel`, `PropertyDeleteViewModel`.
- `PropertyManagement/wwwroot/js/modal-forms.js` — **done**, generic, reused by every future modal (see "Modal pattern" below for the exact contract it implements).
- `PropertyManagement/Views/Account/AccessDenied.cshtml` + `AccountController.AccessDenied()` — **done** (discovered as a gap while testing `[Authorize(Roles=...)]`: ASP.NET Core Identity's cookie scheme redirects an authenticated-but-unauthorized user to `/Account/AccessDenied` by default; without this action/view it was a 404). Needed by every `[Authorize(Roles=...)]` controller, not just this one.

## Requirements (from spec)

- PM can add/edit/remove properties and units (unit number, bedrooms, monthly rent, unit type) **through modals**.
- Applicants browse available units and start an application for one.
- Unit Type is a lookup with Active/Inactive values. An inactive value still displays on a unit already using it but **cannot be selected for any other unit — enforced on the server**, not just hidden client-side.
- A unit whose lease term covers today is not available.

## Modal pattern (implemented — reuse exactly this for every other modal in the app)

Concrete mechanism, built once in `wwwroot/js/modal-forms.js` and `Views/Shared/_Layout.cshtml`, verified live via `curl` for Properties Create/Edit/Delete:

1. `_Layout.cshtml` has one shared modal shell at the bottom of the page: `<div class="modal" id="shared-modal"><div class="modal-dialog"><div class="modal-content" id="shared-modal-content"></div></div></div>`.
2. Any trigger element gets `data-modal-url="/Controller/Action/{id?}"` (works on `<a>`/buttons anywhere). `modal-forms.js` delegates a click handler: `GET`s that URL, drops the returned HTML into `#shared-modal-content`, re-parses jQuery unobtrusive validation, shows the Bootstrap modal. The controller action returns `PartialView(...)` containing the full `.modal-header`/`.modal-body`/`.modal-footer` markup (not just the inner body) — see `_PropertyFormPartial.cshtml`/`_PropertyDeleteConfirmPartial.cshtml`.
3. The partial's `<form>` carries three data attributes: `data-modal-form` (marks it for the submit handler), `data-refresh-url="/Controller/List"` and `data-refresh-target="#some-list-container"` (tells the generic JS what to refresh on success — keeps `modal-forms.js` free of any per-entity knowledge).
4. On submit, `modal-forms.js` `$.valid()`-checks then AJAX-POSTs the serialized form (antiforgery token included automatically — it's a real tag-helper `<form>`, no separate global token needed) and branches on the response's `Content-Type`:
   - **`text/html`** (validation failed — controller returned `PartialView(model)` again with `ModelState` errors) → replace `#shared-modal-content` with it, re-parse validation, modal stays open.
   - **`application/json`** (controller returned `Json(new { success = true })`) → hide the modal, `GET` `data-refresh-url` and replace `data-refresh-target`'s HTML with the response (a view-component-rendered list partial) — no full page reload.
5. Remove/delete goes through the exact same flow: `GET` returns a confirmation partial (not a `confirm()` popup — ToS says "add, edit, and remove... through modals"), `POST` performs the delete and returns the same JSON success shape.

Every future modal (Unit CRUD, Residence History, Review) just needs its own partial(s) + controller actions following this contract — no new JS.

## Steps

0. ~~Properties CRUD~~ done (`PropertiesController`, `IPropertyService`, modals, `PropertiesListViewComponent`) — see "Where this lives" above. Verified live: PM can create/edit/remove via the modal contract above; an Applicant hitting `/Properties/Index` gets redirected to `/Account/AccessDenied` (not a raw 404/500). **Next up:** `UnitType` admin, then `Unit` CRUD nested under a property, then the availability service and applicant browse view (steps 1–6 below, unchanged).
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
