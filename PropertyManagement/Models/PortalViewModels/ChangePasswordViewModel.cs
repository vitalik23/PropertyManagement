using System.ComponentModel.DataAnnotations;

namespace PropertyManagement.Models.PortalViewModels;

public class ChangePasswordViewModel
{
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm new password")]
    [Compare(nameof(NewPassword))]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
