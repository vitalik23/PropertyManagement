using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Infrastructure.Services;
using PropertyManagement.Models;
using System.Diagnostics;

namespace PropertyManagement.Controllers
{
    public class HomeController(IUnitAvailabilityService availabilityService, IPropertyService propertyService, IUnitTypeService unitTypeService) : Controller
    {
        public async Task<IActionResult> Index(Guid? propertyId, Guid? unitTypeId, int? minBedrooms, decimal? minRent, decimal? maxRent)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var units = await availabilityService.GetAvailableUnitsAsync(today, propertyId, unitTypeId, minBedrooms, minRent, maxRent);

            ViewBag.Properties = await propertyService.GetAllAsync();
            ViewBag.UnitTypes = await unitTypeService.GetAllAsync();
            ViewBag.SelectedPropertyId = propertyId;
            ViewBag.SelectedUnitTypeId = unitTypeId;
            ViewBag.SelectedMinBedrooms = minBedrooms;
            ViewBag.SelectedMinRent = minRent;
            ViewBag.SelectedMaxRent = maxRent;

            return View(units);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
