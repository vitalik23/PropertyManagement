using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Domain.Constants;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Services;
using PropertyManagement.Models.ApplicationViewModels;

namespace PropertyManagement.Controllers;

[Authorize(Roles = Roles.Applicant)]
public class ApplicationsController(IApplicationService applicationService, IPropertyService propertyService, UserManager<User> userManager) : Controller
{
    private Guid CurrentUserId => Guid.Parse(userManager.GetUserId(User)!);

    public async Task<IActionResult> Index(ApplicationStatus? status, Guid? propertyId)
    {
        var applications = await applicationService.GetFilteredApplicationsAsync(CurrentUserId, status, propertyId);
        ViewBag.Properties = await propertyService.GetAllAsync();
        ViewBag.SelectedStatus = status;
        ViewBag.SelectedPropertyId = propertyId;
        return View(applications);
    }

    public async Task<IActionResult> ListPartial(ApplicationStatus? status, Guid? propertyId)
    {
        var applications = await applicationService.GetFilteredApplicationsAsync(CurrentUserId, status, propertyId);
        return PartialView("_ApplicationsListPartial", applications);
    }

    public async Task<IActionResult> Start(Guid unitId)
    {
        var application = await applicationService.GetOrStartAsync(unitId, CurrentUserId);
        return RedirectToAction(nameof(Wizard), new { id = application.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Wizard(Guid id)
    {
        var application = await applicationService.GetByIdAsync(id);
        if (application is null || application.ApplicantUserId != CurrentUserId)
        {
            return NotFound();
        }

        return View(BuildViewModel(application, ResumeSection(application)));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Wizard(RentalApplicationWizardViewModel model, string submitAction)
    {
        var application = await applicationService.GetByIdAsync(model.Id);
        if (application is null || application.ApplicantUserId != CurrentUserId)
        {
            return NotFound();
        }

        var editable = IsEditable(application);

        if (!editable)
        {
            return View(BuildViewModel(application, WizardSection.Summary));
        }

        if (submitAction == "back")
        {
            return View(BuildViewModel(application, PreviousSection(model.CurrentSection)));
        }

        if (submitAction == "continue" && model.CurrentSection == WizardSection.ApplicantInfo)
        {
            if (!ModelState.IsValid)
            {
                var invalidModel = BuildViewModel(application, WizardSection.ApplicantInfo);
                invalidModel.FullName = model.FullName;
                invalidModel.PhoneNumber = model.PhoneNumber;
                invalidModel.Email = model.Email;
                invalidModel.CurrentAddress = model.CurrentAddress;
                return View(invalidModel);
            }

            var result = await applicationService.SaveApplicantInfoAsync(application.Id, CurrentUserId, model.FullName, model.PhoneNumber, model.Email, model.CurrentAddress);
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.Error!);
                return View(BuildViewModel(application, WizardSection.ApplicantInfo));
            }

            var refreshed = await applicationService.GetByIdAsync(application.Id);
            return View(BuildViewModel(refreshed!, WizardSection.ResidenceHistory));
        }

        if (submitAction == "continue" && model.CurrentSection == WizardSection.ResidenceHistory)
        {
            var result = await applicationService.ConfirmResidenceHistoryAsync(application.Id, CurrentUserId);
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.Error!);
                return View(BuildViewModel(application, WizardSection.ResidenceHistory));
            }

            var refreshed = await applicationService.GetByIdAsync(application.Id);
            return View(BuildViewModel(refreshed!, WizardSection.Summary));
        }

        if (submitAction == "submit")
        {
            var result = await applicationService.SubmitAsync(application.Id, CurrentUserId);
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, result.Error!);
                var refreshedFail = await applicationService.GetByIdAsync(application.Id);
                return View(BuildViewModel(refreshedFail!, WizardSection.Summary));
            }

            return RedirectToAction(nameof(Index));
        }

        return View(BuildViewModel(application, model.CurrentSection));
    }

    [HttpGet]
    public async Task<IActionResult> ResidenceList(Guid applicationId)
    {
        var application = await applicationService.GetByIdAsync(applicationId);
        if (application is null || application.ApplicantUserId != CurrentUserId)
        {
            return NotFound();
        }

        return ViewComponent("ResidenceList", new { applicationId, isEditable = IsEditable(application) });
    }

    [HttpGet]
    public async Task<IActionResult> AddResidence(Guid applicationId)
    {
        var application = await applicationService.GetByIdAsync(applicationId);
        if (application is null || application.ApplicantUserId != CurrentUserId || !IsEditable(application))
        {
            return NotFound();
        }

        return PartialView("_ResidenceFormPartial", new ResidenceFormViewModel { ApplicationId = applicationId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddResidence(ResidenceFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_ResidenceFormPartial", model);
        }

        var result = await applicationService.AddResidenceAsync(model.ApplicationId, CurrentUserId, model.Address, model.LandlordName, model.LandlordPhone, model.MoveInDate, model.MoveOutDate);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(nameof(model.MoveOutDate), result.Error!);
            return PartialView("_ResidenceFormPartial", model);
        }

        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> EditResidence(Guid id)
    {
        var residence = await applicationService.GetResidenceAsync(id);
        if (residence is null || residence.RentalApplication.ApplicantUserId != CurrentUserId || !IsEditable(residence.RentalApplication))
        {
            return NotFound();
        }

        var model = new ResidenceFormViewModel
        {
            Id = residence.Id,
            ApplicationId = residence.RentalApplicationId,
            Address = residence.Address,
            LandlordName = residence.LandlordName,
            LandlordPhone = residence.LandlordPhone,
            MoveInDate = residence.MoveInDate,
            MoveOutDate = residence.MoveOutDate
        };

        return PartialView("_ResidenceFormPartial", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditResidence(ResidenceFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_ResidenceFormPartial", model);
        }

        var result = await applicationService.UpdateResidenceAsync(model.Id!.Value, CurrentUserId, model.Address, model.LandlordName, model.LandlordPhone, model.MoveInDate, model.MoveOutDate);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(nameof(model.MoveOutDate), result.Error!);
            return PartialView("_ResidenceFormPartial", model);
        }

        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> DeleteResidence(Guid id)
    {
        var residence = await applicationService.GetResidenceAsync(id);
        if (residence is null || residence.RentalApplication.ApplicantUserId != CurrentUserId || !IsEditable(residence.RentalApplication))
        {
            return NotFound();
        }

        return PartialView("_ResidenceDeleteConfirmPartial", new ResidenceDeleteViewModel
        {
            Id = residence.Id,
            ApplicationId = residence.RentalApplicationId,
            Address = residence.Address
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteResidence(ResidenceDeleteViewModel model)
    {
        var result = await applicationService.DeleteResidenceAsync(model.Id, CurrentUserId);
        if (!result.Succeeded)
        {
            return NotFound();
        }

        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> Withdraw(Guid id)
    {
        var application = await applicationService.GetByIdAsync(id);
        if (application is null || application.ApplicantUserId != CurrentUserId)
        {
            return NotFound();
        }

        return PartialView("_WithdrawConfirmPartial", new WithdrawConfirmViewModel
        {
            Id = application.Id,
            UnitDisplay = $"{application.Unit.Property.Name} — Unit {application.Unit.UnitNumber}"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(WithdrawConfirmViewModel model)
    {
        var result = await applicationService.WithdrawAsync(model.Id, CurrentUserId);
        if (!result.Succeeded)
        {
            return NotFound();
        }

        return Json(new { success = true });
    }

    private RentalApplicationWizardViewModel BuildViewModel(RentalApplication application, WizardSection section)
    {
        return new RentalApplicationWizardViewModel
        {
            Id = application.Id,
            CurrentSection = section,
            IsEditable = IsEditable(application),
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
        };
    }

    private static WizardSection ResumeSection(RentalApplication application)
    {
        if (!IsEditable(application))
        {
            return WizardSection.Summary;
        }

        if (application.ApplicantInfoCompletedAt is null)
        {
            return WizardSection.ApplicantInfo;
        }

        return application.ResidenceHistoryCompletedAt is null
            ? WizardSection.ResidenceHistory
            : WizardSection.Summary;
    }

    private static WizardSection PreviousSection(WizardSection current) => current switch
    {
        WizardSection.ResidenceHistory => WizardSection.ApplicantInfo,
        WizardSection.Summary => WizardSection.ResidenceHistory,
        _ => WizardSection.ApplicantInfo
    };

    private static bool IsEditable(RentalApplication application)
        => application.Status is ApplicationStatus.Draft or ApplicationStatus.Returned;
}
