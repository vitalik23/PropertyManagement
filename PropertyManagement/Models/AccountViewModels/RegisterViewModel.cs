using System.ComponentModel.DataAnnotations;
using PropertyManagement.Domain.Constants;

namespace PropertyManagement.Models.AccountViewModels;

public class RegisterViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    [AllowedValues(Roles.Applicant, Roles.PropertyManager)]
    public string Role { get; set; } = string.Empty;
}
