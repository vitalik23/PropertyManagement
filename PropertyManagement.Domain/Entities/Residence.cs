using PropertyManagement.Domain.Entities.Base;

namespace PropertyManagement.Domain.Entities;

public class Residence : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Guid RentalApplicationId { get; set; }
    public RentalApplication RentalApplication { get; set; } = null!;

    public string Address { get; set; } = string.Empty;
    public string LandlordName { get; set; } = string.Empty;
    public string LandlordPhone { get; set; } = string.Empty;
    public DateOnly MoveInDate { get; set; }
    public DateOnly MoveOutDate { get; set; }
}
