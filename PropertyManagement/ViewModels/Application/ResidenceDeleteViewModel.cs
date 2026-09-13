namespace PropertyManagement.ViewModels.Application;

public class ResidenceDeleteViewModel
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public string Address { get; set; } = string.Empty;
    public int Version { get; set; }
}
