using PropertyManagement.Domain.Entities.Base;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Domain.Entities;

public class RentalApplication : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Draft;

    public Guid UnitId { get; set; }
    public Unit Unit { get; set; } = null!;

    public Guid ApplicantUserId { get; set; }
    public User ApplicantUser { get; set; } = null!;

    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CurrentAddress { get; set; } = string.Empty;

    public DateTime? ApplicantInfoCompletedAt { get; set; }
    public DateTime? ResidenceHistoryCompletedAt { get; set; }

    public ICollection<Residence> Residences { get; set; } = new List<Residence>();
    public Lease? Lease { get; set; }
    public ICollection<ApplicationStatusHistory> StatusHistory { get; set; } = new List<ApplicationStatusHistory>();
}
