using System.ComponentModel.DataAnnotations;

namespace PropertyManagement.Models.ApplicationViewModels;

public enum WizardSection
{
    ApplicantInfo = 0,
    ResidenceHistory = 1,
    Summary = 2
}

public class RentalApplicationWizardViewModel
{
    public Guid Id { get; set; }
    public WizardSection CurrentSection { get; set; }
    public bool IsEditable { get; set; }
    public string UnitDisplay { get; set; } = string.Empty;
    public bool ApplicantInfoSaved { get; set; }
    public bool ResidenceHistorySaved { get; set; }

    [Required]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Phone number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Current address")]
    public string CurrentAddress { get; set; } = string.Empty;

    public List<ResidenceListItem> Residences { get; set; } = [];
}
