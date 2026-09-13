using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Application.Services;
using PropertyManagement.ViewModels.Portal;

namespace PropertyManagement.Controllers;

[Authorize]
public class PortalController(IAuthService authService, UserManager<User> userManager) : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> Profile()
    {
        var model = await BuildProfileViewModelAsync();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(ProfileInfoViewModel info)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var result = await authService.UpdateProfileAsync(user.Id, info.Email, info.PhoneNumber);
            if (result.Succeeded)
            {
                TempData["ProfileSuccess"] = "Profile updated.";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        var model = await BuildProfileViewModelAsync();
        model.Info.Email = info.Email;
        model.Info.PhoneNumber = info.PhoneNumber;
        return View(nameof(Profile), model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel password)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            var result = await authService.ChangePasswordAsync(user.Id, password.CurrentPassword, password.NewPassword);
            if (result.Succeeded)
            {
                TempData["ProfileSuccess"] = "Password changed.";
                return RedirectToAction(nameof(Profile));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        var model = await BuildProfileViewModelAsync();
        model.Password = password;
        return View(nameof(Profile), model);
    }

    private async Task<ProfileViewModel> BuildProfileViewModelAsync()
    {
        var user = await userManager.GetUserAsync(User);
        var roles = user is null ? [] : await userManager.GetRolesAsync(user);

        return new ProfileViewModel
        {
            Info = new ProfileInfoViewModel
            {
                Email = user?.Email ?? string.Empty,
                PhoneNumber = user?.PhoneNumber,
                Role = roles.FirstOrDefault() ?? string.Empty
            }
        };
    }
}
