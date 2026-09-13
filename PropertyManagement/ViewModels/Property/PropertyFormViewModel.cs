using System.ComponentModel.DataAnnotations;

namespace PropertyManagement.ViewModels.Property;

public class PropertyFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Address line 1")]
    public string AddressLine1 { get; set; } = string.Empty;

    [Display(Name = "Address line 2")]
    public string? AddressLine2 { get; set; }

    [Required]
    public string City { get; set; } = string.Empty;

    [Required]
    public string State { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Zip code")]
    [RegularExpression(@"^\d{5}(-\d{4})?$", ErrorMessage = "Enter a valid ZIP code (12345 or 12345-6789).")]
    public string ZipCode { get; set; } = string.Empty;
}
