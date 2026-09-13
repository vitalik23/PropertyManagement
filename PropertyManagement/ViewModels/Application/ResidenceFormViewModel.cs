using System.ComponentModel.DataAnnotations;

namespace PropertyManagement.ViewModels.Application;

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
    [DataType(DataType.PhoneNumber)]
    [RegularExpression(@"^\+?[0-9()\-\s]{7,20}$", ErrorMessage = "Enter a valid phone number.")]
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
