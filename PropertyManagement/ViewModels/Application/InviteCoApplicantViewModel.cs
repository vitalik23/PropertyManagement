using System.ComponentModel.DataAnnotations;

namespace PropertyManagement.ViewModels.Application;

public class InviteCoApplicantViewModel
{
    [Required]
    public Guid ApplicationId { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
