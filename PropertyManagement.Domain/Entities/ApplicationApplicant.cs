using PropertyManagement.Domain.Entities.Base;

namespace PropertyManagement.Domain.Entities;

/// <summary>
/// A co-applicant invited onto a <see cref="RentalApplication"/> beyond its primary
/// <see cref="RentalApplication.ApplicantUserId"/>. Any user listed here has the same
/// view/edit rights on the application as the primary applicant.
/// </summary>
public class ApplicationApplicant : IBaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Guid RentalApplicationId { get; set; }
    public RentalApplication RentalApplication { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
