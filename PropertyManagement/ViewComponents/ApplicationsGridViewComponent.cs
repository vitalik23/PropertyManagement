using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Domain.Constants;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Application.Services;

namespace PropertyManagement.ViewComponents;

public class ApplicationsGridViewModel
{
    public string Title { get; set; } = "Applications";
    public List<PropertyManagement.Domain.Entities.Property> Properties { get; set; } = [];
    public bool IsPropertyManager { get; set; }
    public string DetailUrlTemplate { get; set; } = string.Empty;
    public bool CanWithdraw { get; set; }
    public List<ApplicationStatus> Statuses { get; set; } = [.. Enum.GetValues<ApplicationStatus>()];
}

public class ApplicationsGridViewComponent(IPropertyService propertyService) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(string title = "Applications")
    {
        var isPropertyManager = User.IsInRole(Roles.PropertyManager);

        return View(new ApplicationsGridViewModel
        {
            Title = title,
            Properties = await propertyService.GetAllAsync(),
            IsPropertyManager = isPropertyManager,
            DetailUrlTemplate = isPropertyManager ? "/Review/Details/" : "/Applications/Wizard/",
            CanWithdraw = !isPropertyManager
        });
    }
}
