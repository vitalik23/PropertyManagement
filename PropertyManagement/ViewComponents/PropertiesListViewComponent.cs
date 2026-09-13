using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Application.Services;

namespace PropertyManagement.ViewComponents;

public class PropertiesListViewComponent(IPropertyService propertyService) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var properties = await propertyService.GetAllAsync();
        return View(properties);
    }
}
