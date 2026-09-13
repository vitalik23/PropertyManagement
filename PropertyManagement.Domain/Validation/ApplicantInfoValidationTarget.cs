using System.ComponentModel.DataAnnotations;

namespace PropertyManagement.Domain.Validation;

/// <summary>
/// Defines the Applicant Information section's validation rules once. Reused both as the base
/// class of the wizard's postback ViewModel (so ASP.NET Core ModelState validates a live submit)
/// and, independently, to re-validate already-persisted data (so the Summary can list issues and
/// Submit can be gated on the current state, not just what was true at the last successful save).
/// </summary>
public class ApplicantInfoValidationTarget
{
    [Required]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Phone number")]
    [DataType(DataType.PhoneNumber)]
    [RegularExpression(@"^\+?[0-9()\-\s]{7,20}$", ErrorMessage = "Enter a valid phone number.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Current address")]
    public string CurrentAddress { get; set; } = string.Empty;
}
