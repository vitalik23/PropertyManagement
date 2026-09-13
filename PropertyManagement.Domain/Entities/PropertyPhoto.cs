using PropertyManagement.Domain.Entities.Base;

namespace PropertyManagement.Domain.Entities;

public class PropertyPhoto : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Guid PropertyId { get; set; }
    public Property Property { get; set; } = null!;

    public string Url { get; set; } = string.Empty;
    public string BlobName { get; set; } = string.Empty;
}
