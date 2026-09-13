using System.ComponentModel.DataAnnotations;

namespace PropertyManagement.ViewModels.UnitType;

public class UnitTypeFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}
