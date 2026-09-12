using PropertyManagement.Domain.Entities.Base;

namespace PropertyManagement.Domain.Entities;

public class Lease : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Guid UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public Guid RentalApplicationId { get; set; }
    public RentalApplication RentalApplication { get; set; } = null!;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}
