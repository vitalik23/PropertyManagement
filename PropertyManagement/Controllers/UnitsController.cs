using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PropertyManagement.Domain.Constants;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Infrastructure.Services;
using PropertyManagement.Models;
using PropertyManagement.Models.UnitViewModels;

namespace PropertyManagement.Controllers;

[Authorize(Roles = Roles.PropertyManager)]
public class UnitsController(IUnitService unitService, IPropertyService propertyService) : Controller
{
    public async Task<IActionResult> Index(Guid propertyId)
    {
        var property = await propertyService.GetByIdAsync(propertyId);
        if (property is null)
        {
            return NotFound();
        }

        ViewData["PropertyId"] = property.Id;
        ViewData["PropertyName"] = property.Name;

        return View();
    }

    public IActionResult List(Guid propertyId)
    {
        return ViewComponent("UnitsList", new { propertyId });
    }

    [HttpGet]
    public async Task<IActionResult> Create(Guid propertyId)
    {
        var model = new UnitFormViewModel { PropertyId = propertyId };
        await PopulateUnitTypeOptionsAsync(model);

        return PartialView("_UnitFormPartial", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UnitFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateUnitTypeOptionsAsync(model);
            return PartialView("_UnitFormPartial", model);
        }

        var result = await unitService.CreateAsync(model.PropertyId, model.UnitNumber, model.Bedrooms, model.MonthlyRent, model.UnitTypeId);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(nameof(model.UnitTypeId), result.Error!);
            await PopulateUnitTypeOptionsAsync(model);
            return PartialView("_UnitFormPartial", model);
        }

        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var unit = await unitService.GetByIdAsync(id);
        if (unit is null)
        {
            return NotFound();
        }

        var model = new UnitFormViewModel
        {
            Id = unit.Id,
            PropertyId = unit.PropertyId,
            UnitNumber = unit.UnitNumber,
            Bedrooms = unit.Bedrooms,
            MonthlyRent = unit.MonthlyRent,
            UnitTypeId = unit.UnitTypeId
        };
        await PopulateUnitTypeOptionsAsync(model);

        return PartialView("_UnitFormPartial", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UnitFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateUnitTypeOptionsAsync(model);
            return PartialView("_UnitFormPartial", model);
        }

        var result = await unitService.UpdateAsync(model.Id!.Value, model.UnitNumber, model.Bedrooms, model.MonthlyRent, model.UnitTypeId);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(nameof(model.UnitTypeId), result.Error!);
            await PopulateUnitTypeOptionsAsync(model);
            return PartialView("_UnitFormPartial", model);
        }

        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> Delete(Guid id)
    {
        var unit = await unitService.GetByIdAsync(id);
        if (unit is null)
        {
            return NotFound();
        }

        return PartialView("_UnitDeleteConfirmPartial", new UnitDeleteViewModel
        {
            Id = unit.Id,
            PropertyId = unit.PropertyId,
            UnitNumber = unit.UnitNumber
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(UnitDeleteViewModel model)
    {
        var deleted = await unitService.DeleteAsync(model.Id);
        if (!deleted)
        {
            return NotFound();
        }

        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> Photos(Guid id)
    {
        var unit = await unitService.GetByIdAsync(id);
        if (unit is null)
        {
            return NotFound();
        }

        var photos = await unitService.GetPhotosAsync(id);
        return PartialView("_UnitPhotosPartial", new UnitPhotosViewModel { UnitId = id, Photos = photos });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadPhoto(Guid unitId, IFormFile? file)
    {
        var unit = await unitService.GetByIdAsync(unitId);
        if (unit is null)
        {
            return NotFound();
        }

        var error = PhotoUploadValidation.Validate(file);
        if (error is not null)
        {
            var photos = await unitService.GetPhotosAsync(unitId);
            return PartialView("_UnitPhotosPartial", new UnitPhotosViewModel { UnitId = unitId, Photos = photos, Error = error });
        }

        await using (var stream = file!.OpenReadStream())
        {
            await unitService.AddPhotoAsync(unitId, stream, file.FileName, file.ContentType);
        }

        var refreshedPhotos = await unitService.GetPhotosAsync(unitId);
        return PartialView("_UnitPhotosPartial", new UnitPhotosViewModel { UnitId = unitId, Photos = refreshedPhotos });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePhoto(Guid photoId, Guid unitId)
    {
        await unitService.RemovePhotoAsync(photoId);

        var photos = await unitService.GetPhotosAsync(unitId);
        return PartialView("_UnitPhotosPartial", new UnitPhotosViewModel { UnitId = unitId, Photos = photos });
    }

    private async Task PopulateUnitTypeOptionsAsync(UnitFormViewModel model)
    {
        var unitTypes = await unitService.GetSelectableUnitTypesAsync(model.Id.HasValue ? model.UnitTypeId : null);
        model.UnitTypeOptions = unitTypes
            .Select(t => new SelectListItem(t.IsActive ? t.Name : $"{t.Name} (Inactive)", t.Id.ToString()))
            .ToList();
    }
}
