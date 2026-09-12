using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Infrastructure.Services;

namespace PropertyManagement.ViewComponents;

public class UnitTypesListViewComponent(IUnitTypeService unitTypeService) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var unitTypes = await unitTypeService.GetAllAsync();
        return View(unitTypes);
    }
}
