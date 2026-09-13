using System.ComponentModel.DataAnnotations;
using PropertyManagement.Application.Services;

namespace PropertyManagement.ViewModels.Review;

public class ReviewViewModel
{
    [Required]
    public Guid ApplicationId { get; set; }

    [Required]
    public ReviewOutcome Outcome { get; set; }

    public string? Comment { get; set; }
}
