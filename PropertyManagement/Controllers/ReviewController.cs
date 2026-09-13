using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Domain.Constants;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Application.Services;
using PropertyManagement.ViewModels.Application;
using PropertyManagement.ViewModels.Review;

namespace PropertyManagement.Controllers;

[Authorize(Roles = Roles.PropertyManager)]
public class ReviewController(
    IApplicationService applicationService,
    IApplicationReviewService reviewService,
    UserManager<User> userManager) : Controller
{
    private Guid CurrentUserId => Guid.Parse(userManager.GetUserId(User)!);

    public IActionResult Index() => View();

    public async Task<IActionResult> Details(Guid id)
    {
        var application = await applicationService.GetByIdAsync(id);
        if (application is null)
        {
            return NotFound();
        }

        return View(await BuildDetailsViewModelAsync(application));
    }

    public async Task<IActionResult> DetailsPartial(Guid id)
    {
        var application = await applicationService.GetByIdAsync(id);
        if (application is null)
        {
            return NotFound();
        }

        return PartialView("_ApplicationDetailsPartial", await BuildDetailsViewModelAsync(application));
    }

    [HttpGet]
    public async Task<IActionResult> Review(Guid id)
    {
        var application = await applicationService.GetByIdAsync(id);
        if (application is null || application.Status != ApplicationStatus.Submitted)
        {
            return NotFound();
        }

        return PartialView("_ReviewModalPartial", new ReviewViewModel { ApplicationId = id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(ReviewViewModel model)
    {
        if (model.Outcome is ReviewOutcome.Return or ReviewOutcome.Deny && string.IsNullOrWhiteSpace(model.Comment))
        {
            ModelState.AddModelError(nameof(model.Comment), "A comment is required for Return or Deny.");
        }

        if (!ModelState.IsValid)
        {
            return PartialView("_ReviewModalPartial", model);
        }

        var result = await reviewService.ReviewAsync(model.ApplicationId, model.Outcome, model.Comment, CurrentUserId);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return PartialView("_ReviewModalPartial", model);
        }

        return Json(new { success = true });
    }

    private async Task<ApplicationDetailsViewModel> BuildDetailsViewModelAsync(RentalApplication application)
    {
        var history = await applicationService.GetStatusHistoryAsync(application.Id);

        return new ApplicationDetailsViewModel
        {
            Id = application.Id,
            Status = application.Status,
            UnitDisplay = $"{application.Unit.Property.Name} — Unit {application.Unit.UnitNumber}",
            ApplicantEmail = application.ApplicantUser.Email ?? string.Empty,
            Wizard = new RentalApplicationWizardViewModel
            {
                Id = application.Id,
                CurrentSection = WizardSection.Summary,
                IsEditable = false,
                UnitDisplay = $"{application.Unit.Property.Name} — Unit {application.Unit.UnitNumber}",
                ApplicantInfoSaved = application.ApplicantInfoCompletedAt.HasValue,
                ResidenceHistorySaved = application.ResidenceHistoryCompletedAt.HasValue,
                FullName = application.FullName,
                PhoneNumber = application.PhoneNumber,
                Email = application.Email,
                CurrentAddress = application.CurrentAddress,
                Residences = application.Residences.Select(r => new ResidenceListItem
                {
                    Id = r.Id,
                    Address = r.Address,
                    LandlordName = r.LandlordName,
                    LandlordPhone = r.LandlordPhone,
                    MoveInDate = r.MoveInDate,
                    MoveOutDate = r.MoveOutDate
                }).ToList()
            },
            History = history
        };
    }
}
