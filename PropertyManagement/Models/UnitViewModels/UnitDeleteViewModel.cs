namespace PropertyManagement.Models.UnitViewModels;

public class UnitDeleteViewModel
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string UnitNumber { get; set; } = string.Empty;
}
