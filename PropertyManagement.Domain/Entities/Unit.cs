using PropertyManagement.Domain.Entities.Base;

namespace PropertyManagement.Domain.Entities;

public class Unit : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string UnitNumber { get; set; } = string.Empty;
    public int Bedrooms { get; set; }
    public decimal MonthlyRent { get; set; }
    public bool IsRemoved { get; set; }

    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;

    public Guid UnitTypeId { get; set; }
    public UnitType UnitType { get; set; } = null!;

    public ICollection<RentalApplication> RentalApplications { get; set; } = new List<RentalApplication>();
    public ICollection<Lease> Leases { get; set; } = new List<Lease>();
    public ICollection<UnitPhoto> Photos { get; set; } = new List<UnitPhoto>();
}
