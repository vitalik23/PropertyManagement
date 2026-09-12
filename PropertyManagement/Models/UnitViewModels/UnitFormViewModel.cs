using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PropertyManagement.Models.UnitViewModels;

public class UnitFormViewModel
{
    public Guid? Id { get; set; }

    [Required]
    public Guid PropertyId { get; set; }

    [Required]
    [Display(Name = "Unit number")]
    public string UnitNumber { get; set; } = string.Empty;

    [Required]
    [Range(0, 20)]
    public int Bedrooms { get; set; }

    [Required]
    [Display(Name = "Monthly rent")]
    public decimal MonthlyRent { get; set; }

    [Required]
    [Display(Name = "Unit type")]
    public Guid UnitTypeId { get; set; }

    public List<SelectListItem> UnitTypeOptions { get; set; } = [];
}
