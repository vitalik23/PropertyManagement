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
public class ApplicationsController(IApplicationService applicationService, UserManager<User> userManager) : Controller
{
    private Guid CurrentUserId => Guid.Parse(userManager.GetUserId(User)!);

    public IActionResult Index() => View();

    public async Task<IActionResult> Start(Guid unitId)
    {
        var application = await applicationService.GetOrStartAsync(unitId, CurrentUserId);
        return RedirectToAction(nameof(Wizard), new { id = application.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Wizard(Guid id)
    {
        var application = await applicationService.GetByIdAsync(id);
        if (application is null || !IsApplicantOnApplication(application, CurrentUserId))
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
        if (application is null || !IsApplicantOnApplication(application, CurrentUserId))
        {
            return NotFound();
        }

        var editable = IsEditable(application);

        if (!editable)
        {
            ModelState.Clear();
            return View(BuildViewModel(application, WizardSection.Summary));
        }

        if (submitAction == "back")
        {
            ModelState.Clear();
            return View(BuildViewModel(application, PreviousSection(model.CurrentSection)));
        }

        if (submitAction == "continue" && model.CurrentSection == WizardSection.ApplicantInfo)
        {
            // Save whatever was submitted, valid or not (save-with-errors) — the section is only
            // ever rejected outright when the application itself is no longer editable/owned, or
            // when another applicant already saved this section first (stale version).
            var result = await applicationService.SaveApplicantInfoAsync(application.Id, CurrentUserId, model.FullName, model.PhoneNumber, model.Email, model.CurrentAddress, model.ApplicantInfoVersion);
            if (!result.Succeeded)
            {
                // ModelState still holds automatic [Required] errors from binding THIS request's
                // own posted values (which lost the conflict and were never saved) — clear it so
                // asp-validation-for doesn't attach those to the freshly-rebuilt view model below,
                // which instead reflects whichever save actually won.
                ModelState.Clear();
                ModelState.AddModelError(string.Empty, result.Error!);
                return View(BuildViewModel(application, WizardSection.ApplicantInfo));
            }

            var refreshed = await applicationService.GetByIdAsync(application.Id);
            var applicantInfoErrors = applicationService.ValidateApplicantInfo(refreshed!);

            if (applicantInfoErrors.Count > 0)
            {
                ModelState.Clear();
                foreach (var error in applicantInfoErrors)
                {
                    ModelState.AddModelError(error.Field, error.Message);
                }

                return View(BuildViewModel(refreshed!, WizardSection.ApplicantInfo));
            }

            return View(BuildViewModel(refreshed!, WizardSection.ResidenceHistory));
        }

        if (submitAction == "continue" && model.CurrentSection == WizardSection.ResidenceHistory)
        {
            var result = await applicationService.ConfirmResidenceHistoryAsync(application.Id, CurrentUserId, model.ResidenceHistoryVersion);
            if (!result.Succeeded)
            {
                ModelState.Clear();
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
                ModelState.Clear();
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
        if (application is null || !IsApplicantOnApplication(application, CurrentUserId))
        {
            return NotFound();
        }

        return ViewComponent("ResidenceList", new { applicationId, isEditable = IsEditable(application) });
    }

    [HttpGet]
    public async Task<IActionResult> CoApplicantsList(Guid applicationId)
    {
        var application = await applicationService.GetByIdAsync(applicationId);
        if (application is null || !IsApplicantOnApplication(application, CurrentUserId))
        {
            return NotFound();
        }

        return PartialView("_CoApplicantsListPartial", BuildViewModel(application, WizardSection.Summary));
    }

    [HttpGet]
    public async Task<IActionResult> InviteCoApplicant(Guid applicationId)
    {
        var application = await applicationService.GetByIdAsync(applicationId);
        if (application is null || !IsApplicantOnApplication(application, CurrentUserId) || !IsEditable(application))
        {
            return NotFound();
        }

        return PartialView("_InviteCoApplicantPartial", new InviteCoApplicantViewModel { ApplicationId = applicationId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> InviteCoApplicant(InviteCoApplicantViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return PartialView("_InviteCoApplicantPartial", model);
        }

        var result = await applicationService.AddCoApplicantAsync(model.ApplicationId, CurrentUserId, model.Email);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return PartialView("_InviteCoApplicantPartial", model);
        }

        return Json(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> AddResidence(Guid applicationId)
    {
        var application = await applicationService.GetByIdAsync(applicationId);
        if (application is null || !IsApplicantOnApplication(application, CurrentUserId) || !IsEditable(application))
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
        if (residence is null || !IsApplicantOnApplication(residence.RentalApplication, CurrentUserId) || !IsEditable(residence.RentalApplication))
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
            MoveOutDate = residence.MoveOutDate,
            Version = residence.Version
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

        var result = await applicationService.UpdateResidenceAsync(model.Id!.Value, CurrentUserId, model.Address, model.LandlordName, model.LandlordPhone, model.MoveInDate, model.MoveOutDate, model.Version);
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
        if (residence is null || !IsApplicantOnApplication(residence.RentalApplication, CurrentUserId) || !IsEditable(residence.RentalApplication))
        {
            return NotFound();
        }

        return PartialView("_ResidenceDeleteConfirmPartial", new ResidenceDeleteViewModel
        {
            Id = residence.Id,
            ApplicationId = residence.RentalApplicationId,
            Address = residence.Address,
            Version = residence.Version
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteResidence(ResidenceDeleteViewModel model)
    {
        var result = await applicationService.DeleteResidenceAsync(model.Id, CurrentUserId, model.Version);
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
        if (application is null || !IsApplicantOnApplication(application, CurrentUserId))
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
        var applicantInfoErrors = applicationService.ValidateApplicantInfo(application);
        var residenceErrors = applicationService.ValidateResidenceHistory(application);

        return new RentalApplicationWizardViewModel
        {
            Id = application.Id,
            CurrentSection = section,
            IsEditable = IsEditable(application),
            UnitDisplay = $"{application.Unit.Property.Name} — Unit {application.Unit.UnitNumber}",
            ApplicantInfoSaved = applicantInfoErrors.Count == 0,
            ResidenceHistorySaved = residenceErrors.Count == 0,
            BlockingIssues = [.. applicantInfoErrors, .. residenceErrors],
            ApplicantInfoVersion = application.ApplicantInfoVersion,
            ResidenceHistoryVersion = application.ResidenceHistoryVersion,
            CoApplicantEmails = application.CoApplicants.Select(c => c.User.Email ?? string.Empty).ToList(),
            FullName = application.FullName,
            PhoneNumber = application.PhoneNumber,
            // Default to the account's own email until the applicant actually saves this section —
            // it's what they'll almost always want, and they can still edit it before saving.
            Email = string.IsNullOrWhiteSpace(application.Email) ? (User.Identity?.Name ?? string.Empty) : application.Email,
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

    private static bool IsApplicantOnApplication(RentalApplication application, Guid userId)
        => application.ApplicantUserId == userId || application.CoApplicants.Any(c => c.UserId == userId);
}
