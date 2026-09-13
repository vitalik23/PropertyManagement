using PropertyManagement.Domain.Entities.Base;

namespace PropertyManagement.Domain.Entities;

public class UnitPhoto : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Guid UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public string Url { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
}
