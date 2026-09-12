using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Infrastructure.Services;
using PropertyManagement.Models.ApplicationViewModels;

namespace PropertyManagement.ViewComponents;

public class ResidenceListViewComponentModel
{
    public Guid ApplicationId { get; set; }
    public bool IsEditable { get; set; }
    public List<ResidenceListItem> Residences { get; set; } = [];
}

public class ResidenceListViewComponent(IApplicationService applicationService) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(Guid applicationId, bool isEditable)
    {
        var application = await applicationService.GetByIdAsync(applicationId);
        var residences = application?.Residences
            .Select(r => new ResidenceListItem
            {
                Id = r.Id,
                Address = r.Address,
                LandlordName = r.LandlordName,
                LandlordPhone = r.LandlordPhone,
                MoveInDate = r.MoveInDate,
                MoveOutDate = r.MoveOutDate
            })
            .ToList() ?? [];

        return View(new ResidenceListViewComponentModel
        {
            ApplicationId = applicationId,
            IsEditable = isEditable,
            Residences = residences
        });
    }
}
