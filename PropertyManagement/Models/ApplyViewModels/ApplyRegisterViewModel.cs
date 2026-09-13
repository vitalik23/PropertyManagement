using System.ComponentModel.DataAnnotations;

namespace PropertyManagement.Models.ApplyViewModels;

public class ApplyRegisterViewModel
{
    [Required]
    public Guid UnitId { get; set; }

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
}
