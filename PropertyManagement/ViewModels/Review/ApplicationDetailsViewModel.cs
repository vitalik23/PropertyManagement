using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.ViewModels.Application;

namespace PropertyManagement.ViewModels.Review;

public class ApplicationDetailsViewModel
{
    public Guid Id { get; set; }
    public ApplicationStatus Status { get; set; }
    public string UnitDisplay { get; set; } = string.Empty;
    public string ApplicantEmail { get; set; } = string.Empty;
    public RentalApplicationWizardViewModel Wizard { get; set; } = new();
    public List<ApplicationStatusHistory> History { get; set; } = [];
}
