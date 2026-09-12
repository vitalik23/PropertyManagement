using PropertyManagement.Domain.Entities.Base;

namespace PropertyManagement.Domain.Entities;

public class UnitType : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
