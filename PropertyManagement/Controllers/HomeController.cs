using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Infrastructure.Services;
using PropertyManagement.Models;
using System.Diagnostics;

namespace PropertyManagement.Controllers
{
    public class HomeController(IUnitAvailabilityService availabilityService, IPropertyService propertyService, IUnitTypeService unitTypeService, IUnitService unitService) : Controller
    {
        public async Task<IActionResult> Index(Guid? propertyId, Guid? unitTypeId, int? bedrooms, decimal? minRent, decimal? maxRent)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var units = await availabilityService.GetAvailableUnitsAsync(today, propertyId, unitTypeId, bedrooms, minRent, maxRent);
            var rentRange = await availabilityService.GetRentRangeAsync(today);

            ViewBag.Properties = await propertyService.GetAllAsync();
            ViewBag.UnitTypes = await unitTypeService.GetAllAsync();
            ViewBag.SelectedPropertyId = propertyId;
            ViewBag.SelectedUnitTypeId = unitTypeId;
            ViewBag.SelectedBedrooms = bedrooms;
            // Default the price inputs to the full available range instead of leaving them blank.
            // Rounded to the <input step="50"> in Home/Index.cshtml so the browser doesn't reject
            // the raw (cents-precision) rent values as an invalid step mismatch.
            ViewBag.SelectedMinRent = minRent ?? RoundDownToStep(rentRange.Min);
            ViewBag.SelectedMaxRent = maxRent ?? RoundUpToStep(rentRange.Max);

            return View(units);
        }

        private const decimal RentStep = 50m;

        private static decimal? RoundDownToStep(decimal? value)
            => value is null ? null : Math.Floor(value.Value / RentStep) * RentStep;

        private static decimal? RoundUpToStep(decimal? value)
            => value is null ? null : Math.Ceiling(value.Value / RentStep) * RentStep;

        public async Task<IActionResult> Details(Guid id)
        {
            var unit = await unitService.GetByIdAsync(id);
            if (unit is null || unit.IsRemoved || unit.Property.IsRemoved)
            {
                return NotFound();
            }

            ViewBag.Available = await availabilityService.IsAvailableAsync(id, DateOnly.FromDateTime(DateTime.UtcNow));

            return View(unit);
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
