using PropertyManagement.Domain.Entities.Base;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities;

public class ApplicationStatusHistory : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Guid RentalApplicationId { get; set; }
    public RentalApplication RentalApplication { get; set; } = null!;

    public ApplicationStatus? FromStatus { get; set; }
    public ApplicationStatus ToStatus { get; set; }

    public Guid ChangedByUserId { get; set; }
    public User ChangedByUser { get; set; } = null!;

    public string? Comment { get; set; }
}
