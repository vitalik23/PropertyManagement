using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Infrastructure.Services;

namespace PropertyManagement.ViewComponents;

public class UnitsListViewComponent(IUnitService unitService) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(Guid propertyId)
    {
        var units = await unitService.GetByPropertyIdAsync(propertyId);
        return View(units);
    }
}
