using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Models.PropertyViewModels;

public class PropertyPhotosViewModel
{
    public Guid PropertyId { get; set; }
    public List<PropertyPhoto> Photos { get; set; } = [];
    public string? Error { get; set; }
}
