using System.ComponentModel.DataAnnotations;
using PropertyManagement.Infrastructure.Services;

namespace PropertyManagement.Models.ReviewViewModels;

public class ReviewViewModel
{
    [Required]
    public Guid ApplicationId { get; set; }

    [Required]
    public ReviewOutcome Outcome { get; set; }

    public string? Comment { get; set; }
}
