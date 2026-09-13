using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Domain.Constants;
using PropertyManagement.Application.Services;

namespace PropertyManagement.Controllers;

[Authorize(Roles = Roles.Applicant)]
public class BrowseController(IUnitAvailabilityService availabilityService) : Controller
{
    public async Task<IActionResult> Index()
    {
        var units = await availabilityService.GetAvailableUnitsAsync(DateOnly.FromDateTime(DateTime.UtcNow));
        return View(units);
    }
}
