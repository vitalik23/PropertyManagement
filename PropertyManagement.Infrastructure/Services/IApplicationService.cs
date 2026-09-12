using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;

namespace PropertyManagement.Infrastructure.Services;

public record ApplicationActionResult(bool Succeeded, string? Error = null)
{
    public static ApplicationActionResult Success() => new(true);
    public static ApplicationActionResult Failure(string error) => new(false, error);
}

public interface IApplicationService
{
    Task<RentalApplication> GetOrStartAsync(Guid unitId, Guid applicantUserId);
    Task<RentalApplication?> GetByIdAsync(Guid id);
    Task<List<RentalApplication>> GetMyApplicationsAsync(Guid applicantUserId);
    Task<List<RentalApplication>> GetFilteredApplicationsAsync(Guid? applicantUserId, ApplicationStatus? status, Guid? propertyId);
    Task<List<ApplicationStatusHistory>> GetStatusHistoryAsync(Guid applicationId);
    Task<Residence?> GetResidenceAsync(Guid residenceId);

    Task<ApplicationActionResult> SaveApplicantInfoAsync(Guid id, Guid applicantUserId, string fullName, string phoneNumber, string email, string currentAddress);
    Task<ApplicationActionResult> ConfirmResidenceHistoryAsync(Guid id, Guid applicantUserId);
    Task<ApplicationActionResult> SubmitAsync(Guid id, Guid applicantUserId);
    Task<ApplicationActionResult> WithdrawAsync(Guid id, Guid applicantUserId);

    Task<ApplicationActionResult> AddResidenceAsync(Guid applicationId, Guid applicantUserId, string address, string landlordName, string landlordPhone, DateOnly moveInDate, DateOnly moveOutDate);
    Task<ApplicationActionResult> UpdateResidenceAsync(Guid residenceId, Guid applicantUserId, string address, string landlordName, string landlordPhone, DateOnly moveInDate, DateOnly moveOutDate);
    Task<ApplicationActionResult> DeleteResidenceAsync(Guid residenceId, Guid applicantUserId);
}
