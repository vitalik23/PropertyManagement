using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Domain.Constants;
using PropertyManagement.Application.Services;
using PropertyManagement.ViewModels.UnitType;

namespace PropertyManagement.Controllers;

[Authorize(Roles = Roles.PropertyManager)]
public class UnitTypesController(IUnitTypeService unitTypeService) : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult List()
    {
        return ViewComponent("UnitTypesList");
    }

    [HttpGet]
    public IActionResult Create()
    {
        return PartialView("_UnitTypeFormPartial", new UnitTypeFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UnitTypeFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_UnitTypeFormPartial", model);
        }

        await unitTypeService.CreateAsync(model.Name, model.IsActive);

        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var unitType = await unitTypeService.GetByIdAsync(id);
        if (unitType is null)
        {
            return NotFound();
        }

        var model = new UnitTypeFormViewModel
        {
            Id = unitType.Id,
            Name = unitType.Name,
            IsActive = unitType.IsActive
        };

        return PartialView("_UnitTypeFormPartial", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UnitTypeFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_UnitTypeFormPartial", model);
        }

        var updated = await unitTypeService.UpdateAsync(model.Id!.Value, model.Name, model.IsActive);
        if (!updated)
        {
            return NotFound();
        }

        return Json(new { success = true });
    }
}
