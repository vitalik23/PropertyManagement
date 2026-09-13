using PropertyManagement.Domain.Validation;
using PropertyManagement.Application.Services;

namespace PropertyManagement.ViewModels.Application;

public enum WizardSection
{
    ApplicantInfo = 0,
    ResidenceHistory = 1,
    Summary = 2
}

public class RentalApplicationWizardViewModel : ApplicantInfoValidationTarget
{
    public Guid Id { get; set; }
    public WizardSection CurrentSection { get; set; }
    public bool IsEditable { get; set; }
    public string UnitDisplay { get; set; } = string.Empty;
    public bool ApplicantInfoSaved { get; set; }
    public bool ResidenceHistorySaved { get; set; }
    public List<SectionValidationError> BlockingIssues { get; set; } = [];
    public int ApplicantInfoVersion { get; set; }
    public int ResidenceHistoryVersion { get; set; }
    public List<string> CoApplicantEmails { get; set; } = [];

    public List<ResidenceListItem> Residences { get; set; } = [];
}
