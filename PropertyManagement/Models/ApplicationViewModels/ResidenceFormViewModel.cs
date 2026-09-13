using System.ComponentModel.DataAnnotations;

namespace PropertyManagement.Models.ApplicationViewModels;

public class ResidenceFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    public Guid ApplicationId { get; set; }

    [Required]
    public string Address { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Landlord name")]
    public string LandlordName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Landlord phone")]
    public string LandlordPhone { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Move-in date")]
    [DataType(DataType.Date)]
    public DateOnly MoveInDate { get; set; }

    [Required]
    [Display(Name = "Move-out date")]
    [DataType(DataType.Date)]
    public DateOnly MoveOutDate { get; set; }

    public int Version { get; set; }
}
