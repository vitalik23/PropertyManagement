using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Domain.Constants;
using PropertyManagement.Application.Services;
using PropertyManagement.ViewModels.Apply;

namespace PropertyManagement.Controllers;

/// <summary>
/// Lets an anonymous visitor start renting a unit straight from the public Home page: create an
/// Applicant account inline, then hand off to the existing application flow
/// (<see cref="ApplicationsController.Start"/>) — no application logic is duplicated here.
/// </summary>
public class ApplyController(IUnitAvailabilityService availabilityService, IUnitService unitService, IAuthService authService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Start(Guid unitId)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Start", "Applications", new { unitId });
        }

        var unit = await unitService.GetByIdAsync(unitId);
        if (unit is null)
        {
            return NotFound();
        }

        ViewBag.Unit = unit;
        ViewBag.Unavailable = !await availabilityService.IsAvailableAsync(unitId, DateOnly.FromDateTime(DateTime.UtcNow));

        return View(new ApplyRegisterViewModel { UnitId = unitId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(ApplyRegisterViewModel model)
    {
        var unit = await unitService.GetByIdAsync(model.UnitId);
        if (unit is null)
        {
            return NotFound();
        }

        ViewBag.Unit = unit;

        var available = await availabilityService.IsAvailableAsync(model.UnitId, DateOnly.FromDateTime(DateTime.UtcNow));
        if (!available)
        {
            ViewBag.Unavailable = true;
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await authService.RegisterAsync(model.Email, model.Password, Roles.Applicant);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        return RedirectToAction("Start", "Applications", new { unitId = model.UnitId });
    }
}
