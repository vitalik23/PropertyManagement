using System.ComponentModel.DataAnnotations;

namespace PropertyManagement.ViewModels.Portal;

public class ProfileInfoViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Phone number")]
    [DataType(DataType.PhoneNumber)]
    [RegularExpression(@"^\+?[0-9()\-\s]{7,20}$", ErrorMessage = "Enter a valid phone number.")]
    public string? PhoneNumber { get; set; }

    public string Role { get; set; } = string.Empty;
}
