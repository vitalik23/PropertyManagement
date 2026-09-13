using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Domain.Constants;
using PropertyManagement.Infrastructure.Services;
using PropertyManagement.Models;
using PropertyManagement.Models.PropertyViewModels;

namespace PropertyManagement.Controllers;

[Authorize(Roles = Roles.PropertyManager)]
public class PropertiesController(IPropertyService propertyService) : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult List()
    {
        return ViewComponent("PropertiesList");
    }

    [HttpGet]
    public IActionResult Create()
    {
        return PartialView("_PropertyFormPartial", new PropertyFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PropertyFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_PropertyFormPartial", model);
        }

        await propertyService.CreateAsync(model.Name, model.AddressLine1, model.AddressLine2, model.City, model.State, model.ZipCode);

        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var property = await propertyService.GetByIdAsync(id);
        if (property is null)
        {
            return NotFound();
        }

        var model = new PropertyFormViewModel
        {
            Id = property.Id,
            Name = property.Name,
            AddressLine1 = property.AddressLine1,
            AddressLine2 = property.AddressLine2,
            City = property.City,
            State = property.State,
            ZipCode = property.ZipCode
        };

        return PartialView("_PropertyFormPartial", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PropertyFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_PropertyFormPartial", model);
        }

        var updated = await propertyService.UpdateAsync(model.Id!.Value, model.Name, model.AddressLine1, model.AddressLine2, model.City, model.State, model.ZipCode);
        if (!updated)
        {
            return NotFound();
        }

        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> Delete(Guid id)
    {
        var property = await propertyService.GetByIdAsync(id);
        if (property is null)
        {
            return NotFound();
        }

        return PartialView("_PropertyDeleteConfirmPartial", new PropertyDeleteViewModel { Id = property.Id, Name = property.Name });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(PropertyDeleteViewModel model)
    {
        var deleted = await propertyService.DeleteAsync(model.Id);
        if (!deleted)
        {
            return NotFound();
        }

        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> Photos(Guid id)
    {
        var property = await propertyService.GetByIdAsync(id);
        if (property is null)
        {
            return NotFound();
        }

        var photos = await propertyService.GetPhotosAsync(id);
        return PartialView("_PropertyPhotosPartial", new PropertyPhotosViewModel { PropertyId = id, Photos = photos });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadPhoto(Guid propertyId, IFormFile? file)
    {
        var property = await propertyService.GetByIdAsync(propertyId);
        if (property is null)
        {
            return NotFound();
        }

        var error = PhotoUploadValidation.Validate(file);
        if (error is not null)
        {
            var photos = await propertyService.GetPhotosAsync(propertyId);
            return PartialView("_PropertyPhotosPartial", new PropertyPhotosViewModel { PropertyId = propertyId, Photos = photos, Error = error });
        }

        await using (var stream = file!.OpenReadStream())
        {
            await propertyService.AddPhotoAsync(propertyId, stream, file.FileName, file.ContentType);
        }

        var refreshedPhotos = await propertyService.GetPhotosAsync(propertyId);
        return PartialView("_PropertyPhotosPartial", new PropertyPhotosViewModel { PropertyId = propertyId, Photos = refreshedPhotos });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePhoto(Guid photoId, Guid propertyId)
    {
        await propertyService.RemovePhotoAsync(photoId);

        var photos = await propertyService.GetPhotosAsync(propertyId);
        return PartialView("_PropertyPhotosPartial", new PropertyPhotosViewModel { PropertyId = propertyId, Photos = photos });
    }
}
