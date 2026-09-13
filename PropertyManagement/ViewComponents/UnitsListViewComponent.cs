using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Application.Services;

namespace PropertyManagement.ViewComponents;

public class UnitsListViewModel
{
    public Guid PropertyId { get; set; }
    public List<Unit> Units { get; set; } = [];
}

public class UnitsListViewComponent(IUnitService unitService) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(Guid propertyId)
    {
        var units = await unitService.GetByPropertyIdAsync(propertyId);
        return View(new UnitsListViewModel { PropertyId = propertyId, Units = units });
    }
}
