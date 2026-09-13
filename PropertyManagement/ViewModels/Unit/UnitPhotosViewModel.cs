using PropertyManagement.Domain.Entities;

namespace PropertyManagement.ViewModels.Unit;

public class UnitPhotosViewModel
{
    public Guid UnitId { get; set; }
    public List<UnitPhoto> Photos { get; set; } = [];
    public string? Error { get; set; }
}
