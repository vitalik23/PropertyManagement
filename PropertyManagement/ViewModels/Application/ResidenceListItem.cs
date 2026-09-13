namespace PropertyManagement.ViewModels.Application;

public class ResidenceListItem
{
    public Guid Id { get; set; }
    public string Address { get; set; } = string.Empty;
    public string LandlordName { get; set; } = string.Empty;
    public string LandlordPhone { get; set; } = string.Empty;
    public DateOnly MoveInDate { get; set; }
    public DateOnly MoveOutDate { get; set; }
}
