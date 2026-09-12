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
- `PropertyManagement/Controllers/PropertiesController.cs` — **done**, `[Authorize(Roles = Roles.PropertyManager)]`.
- `PropertyManagement/Controllers/UnitTypesController.cs` — **done**, `[Authorize(Roles = Roles.PropertyManager)]`. `Index`/`List`/`Create`/`Edit` only — no `Delete` (toggle `IsActive` via Edit instead).
- `PropertyManagement/Controllers/UnitsController.cs` — **done**, `[Authorize(Roles = Roles.PropertyManager)]`. Routed with plain conventional routing (not attribute/nested routes as originally suggested — kept consistent with `PropertiesController`'s style): `GET/POST Units/Create?propertyId={id}`, `GET/POST Units/Edit/{id}`, `GET/POST Units/Delete/{id}`, `GET Units/Index?propertyId={id}`, `GET Units/List?propertyId={id}`. `propertyId` travels as a query-string parameter on GETs and a hidden form field on POSTs, not a nested path segment.
- `PropertyManagement.Infrastructure/Services/IUnitTypeService.cs`/`UnitTypeService.cs` and `IUnitService.cs`/`UnitService.cs` — **done**, same primitive-param style as `PropertyService`. `IUnitService.CreateAsync`/`UpdateAsync` return a `UnitSaveResult(bool Succeeded, string? Error)` record (not just `bool`) so the controller can surface the Active/Inactive business-rule violation as a `ModelState` error on `UnitTypeId` — verified live: creating with an Inactive type is rejected with "Selected unit type is inactive and cannot be used for a new unit."; editing to *change to* a different Inactive type is rejected; editing while *keeping* an already-assigned type that's since been deactivated succeeds. `GetSelectableUnitTypesAsync(currentUnitTypeId)` returns Active types plus the current one if it's Inactive (verified: dropdown shows "Studio (Inactive)", selected, after deactivating a type already in use).
- `PropertyManagement/Views/Properties/` — **done**: `Index.cshtml`, `_PropertyFormPartial.cshtml` (shared Create/Edit, `Model.Id.HasValue` picks the action), `_PropertyDeleteConfirmPartial.cshtml`. `Index.cshtml` sets `Layout = "_PortalLayout"` (see below) instead of the default site layout — Properties is reachable only through the Portal now, not the top nav. Each row in `Views/Shared/Components/PropertiesList/Default.cshtml` now has a "Units" link (plain navigation, not a modal trigger) to `Units/Index?propertyId={id}`.
- `PropertyManagement/Views/UnitTypes/` and `Views/Units/` — **done**: same Index + `_XxxFormPartial`/`_XxxDeleteConfirmPartial` shape as Properties. `Units/Index.cshtml` shows the parent property's name in the header and a "Back to Properties" link.
- **Portal shell (done, not originally in this skill but everything PM/Applicant-facing now lives behind it):** `Views/Shared/_PortalLayout.cshtml` (nested layout — `Layout = "_Layout"`, renders a role-aware sidebar + `@RenderBody()`), `PortalController` (`Index`, `Profile` — `[Authorize]`, any role), `Views/Portal/Index.cshtml`/`Profile.cshtml`. The top header (`_Layout.cshtml`) shows a user-icon dropdown linking to `/Portal` when authenticated, hover-opened via real Bootstrap `Dropdown.show()/hide()` calls in `wwwroot/js/site.js` (not CSS `:hover` — an earlier CSS-only attempt broke positioning/caused horizontal overflow because it bypassed Bootstrap's own Popper-based placement). Sidebar currently has Profile (all roles), Properties + Unit Types (PropertyManager only), Logout (all roles). **All UI text is English** — even when a feature is discussed in another language, see `pm-overview`'s "UI language rule". **Any future PM/Applicant-facing page (applicant browse, application wizard, review) should set `Layout = "_PortalLayout"` and add its sidebar link in `_PortalLayout.cshtml`'s role-conditional list, following the `Properties`/`Unit Types` pattern.**
- `PropertyManagement/ViewComponents/` — **done**: `PropertiesListViewComponent` (this is the view component the spec requires somewhere in the app — already satisfied; the applicant browse list can reuse it or get its own later), `UnitTypesListViewComponent`, `UnitsListViewComponent` (`InvokeAsync(Guid propertyId)` — scoped to one property).
- `PropertyManagement/Models/PropertyViewModels/` — **done**: `PropertyFormViewModel`, `PropertyDeleteViewModel`. `Models/UnitTypeViewModels/UnitTypeFormViewModel.cs` and `Models/UnitViewModels/UnitFormViewModel.cs`/`UnitDeleteViewModel.cs` — **done**; `UnitFormViewModel` carries `PropertyId` (hidden field, needed since Units aren't routed by nested path) and `UnitTypeOptions` (`List<SelectListItem>`, rebuilt by the controller — via `IUnitService.GetSelectableUnitTypesAsync` — on every GET and on every invalid/rejected POST, since a posted form doesn't carry the options list back).
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

0. ~~Properties CRUD~~ done (`PropertiesController`, `IPropertyService`, modals, `PropertiesListViewComponent`) — see "Where this lives" above. Verified live: PM can create/edit/remove via the modal contract above; an Applicant hitting `/Properties/Index` gets redirected to `/Account/AccessDenied` (not a raw 404/500).
1. ~~`UnitTypesController`~~ done — Active/Inactive via Edit's checkbox, no `Delete` action exists at all (not even offered in the UI).
2. ~~Unit create/edit dropdown sourcing~~ done — `IUnitService.GetSelectableUnitTypesAsync`, verified live (see "Where this lives" above).
3. ~~Server-side Active/Inactive enforcement~~ done — `UnitSaveResult` from `IUnitService`, verified live for all three cases (reject on create with Inactive, reject on edit *changing to* Inactive, allow edit *keeping* an already-assigned now-Inactive type).
4. **Next up:** Property delete still just removes the row (EF cascade already deletes its Units — configured back in `pm-project-setup`'s entity work, not revisited here); still no block/confirm based on active leases or non-terminal applications, because neither `Lease` nor `RentalApplication` has any UI/rows yet. Revisit once those exist.
5. ~~Availability~~ done — `PropertyManagement.Infrastructure/Services/IUnitAvailabilityService.cs`/`UnitAvailabilityService.cs`: `IsAvailableAsync(Guid unitId, DateOnly today)` and `GetAvailableUnitsAsync(DateOnly today)` (bulk, single DB query with a `NOT EXISTS`-style `Where` — no in-memory filtering). Not yet consumed anywhere except the browse view below; `pm-application-wizard` (submit-time) and `pm-review-list` (approval-time) still need to inject and call it themselves when those get built.
6. ~~Applicant browse view~~ done — `BrowseController` (`[Authorize(Roles = Roles.Applicant)]`, first Applicant-only page in the app), `Views/Browse/Index.cshtml` (`Layout = "_PortalLayout"`, sidebar entry "Browse Units" visible only to `Roles.Applicant`). Plain table, no view component (page is read-only, no modal/AJAX-refresh need — the spec's view-component requirement is already satisfied by `PropertiesListViewComponent`). The "Apply" button is rendered **disabled** (`title="Coming soon"`) — starting an application is `pm-application-wizard`, not built yet, so the button is a placeholder rather than a dead link. Verified live end-to-end: inserted a test `RentalApplication` + covering `Lease` row directly via `sqlcmd` (no UI creates these yet) for the one existing seeded unit — it disappeared from `/Browse/Index`; removed the test rows — it reappeared. Also verified PropertyManager and anonymous users are rejected (`/Account/AccessDenied` and `/Account/Login` respectively) — this page is Applicant-only.

## Acceptance checks

- ✅ Editing a unit whose current type is Inactive still shows that type selected; changing it to a different Inactive type is rejected server-side even if the dropdown were manipulated to offer it.
- ✅ A unit with a lease covering today does not appear in the applicant browse list.
- ✅ Modal validation failures re-render the same modal with errors and no page navigation; successful saves close the modal and the underlying list updates without a full reload.

All three acceptance checks for this skill are now verified live — this skill's scope is essentially complete except for the two "Next up" items in step 4 (Property delete guarding against active leases/applications, deferred until those flows exist).

## Related skills

`pm-overview`, `pm-project-setup` (UnitType lookup seeding), `pm-application-wizard` (consumes availability at submit), `pm-review-list` (consumes availability at approval, creates the Lease).
