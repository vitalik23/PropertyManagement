using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Data;

namespace PropertyManagement.Infrastructure.Services;

public class ApplicationService(ApplicationDbContext db, IUnitAvailabilityService availabilityService) : IApplicationService
{
    public async Task<RentalApplication> GetOrStartAsync(Guid unitId, Guid applicantUserId)
    {
        var existing = await db.RentalApplications.FirstOrDefaultAsync(a =>
            a.UnitId == unitId &&
            a.ApplicantUserId == applicantUserId &&
            (a.Status == ApplicationStatus.Draft || a.Status == ApplicationStatus.Submitted || a.Status == ApplicationStatus.Returned));

        if (existing is not null)
        {
            return existing;
        }

        var application = new RentalApplication
        {
            UnitId = unitId,
            ApplicantUserId = applicantUserId,
            Status = ApplicationStatus.Draft
        };

        db.RentalApplications.Add(application);
        await db.SaveChangesAsync();

        return application;
    }

    public Task<RentalApplication?> GetByIdAsync(Guid id)
        => db.RentalApplications
            .Include(a => a.Residences)
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Include(a => a.Unit).ThenInclude(u => u.UnitType)
            .Include(a => a.ApplicantUser)
            .FirstOrDefaultAsync(a => a.Id == id);

    public Task<List<RentalApplication>> GetMyApplicationsAsync(Guid applicantUserId)
        => db.RentalApplications
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Where(a => a.ApplicantUserId == applicantUserId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

    public Task<List<RentalApplication>> GetFilteredApplicationsAsync(Guid? applicantUserId, ApplicationStatus? status, Guid? propertyId)
    {
        var query = db.RentalApplications
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Include(a => a.ApplicantUser)
            .AsQueryable();

        if (applicantUserId is not null)
        {
            query = query.Where(a => a.ApplicantUserId == applicantUserId);
        }

        if (status is not null)
        {
            query = query.Where(a => a.Status == status);
        }

        if (propertyId is not null)
        {
            query = query.Where(a => a.Unit.PropertyId == propertyId);
        }

        return query.OrderByDescending(a => a.CreatedAt).ToListAsync();
    }

    public Task<List<ApplicationStatusHistory>> GetStatusHistoryAsync(Guid applicationId)
        => db.ApplicationStatusHistories
            .Include(h => h.ChangedByUser)
            .Where(h => h.RentalApplicationId == applicationId)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync();

    public Task<Residence?> GetResidenceAsync(Guid residenceId)
        => db.Residences.Include(r => r.RentalApplication).FirstOrDefaultAsync(r => r.Id == residenceId);

    public async Task<ApplicationActionResult> SaveApplicantInfoAsync(Guid id, Guid applicantUserId, string fullName, string phoneNumber, string email, string currentAddress)
    {
        var application = await GetEditableOwnedAsync(id, applicantUserId);
        if (application is null)
        {
            return ApplicationActionResult.Failure("This application can no longer be edited.");
        }

        application.FullName = fullName;
        application.PhoneNumber = phoneNumber;
        application.Email = email;
        application.CurrentAddress = currentAddress;
        application.ApplicantInfoCompletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return ApplicationActionResult.Success();
    }

    public async Task<ApplicationActionResult> ConfirmResidenceHistoryAsync(Guid id, Guid applicantUserId)
    {
        var application = await GetEditableOwnedAsync(id, applicantUserId);
        if (application is null)
        {
            return ApplicationActionResult.Failure("This application can no longer be edited.");
        }

        application.ResidenceHistoryCompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return ApplicationActionResult.Success();
    }

    public async Task<ApplicationActionResult> SubmitAsync(Guid id, Guid applicantUserId)
    {
        var application = await db.RentalApplications
            .Include(a => a.Unit)
            .FirstOrDefaultAsync(a => a.Id == id && a.ApplicantUserId == applicantUserId);

        if (application is null)
        {
            return ApplicationActionResult.Failure("Application not found.");
        }

        if (application.Status is not (ApplicationStatus.Draft or ApplicationStatus.Returned))
        {
            return ApplicationActionResult.Failure("This application can no longer be edited.");
        }

        if (application.ApplicantInfoCompletedAt is null || application.ResidenceHistoryCompletedAt is null)
        {
            return ApplicationActionResult.Failure("Please complete both sections before submitting.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (!await availabilityService.IsAvailableAsync(application.UnitId, today))
        {
            return ApplicationActionResult.Failure("This unit is no longer available.");
        }

        var fromStatus = application.Status;
        application.Status = ApplicationStatus.Submitted;

        db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
        {
            RentalApplicationId = application.Id,
            FromStatus = fromStatus,
            ToStatus = ApplicationStatus.Submitted,
            ChangedByUserId = applicantUserId
        });

        await db.SaveChangesAsync();

        return ApplicationActionResult.Success();
    }

    public async Task<ApplicationActionResult> WithdrawAsync(Guid id, Guid applicantUserId)
    {
        var application = await db.RentalApplications.FirstOrDefaultAsync(a => a.Id == id && a.ApplicantUserId == applicantUserId);
        if (application is null)
        {
            return ApplicationActionResult.Failure("Application not found.");
        }

        if (application.Status is ApplicationStatus.Approved or ApplicationStatus.Denied or ApplicationStatus.Withdrawn)
        {
            return ApplicationActionResult.Failure("This application can no longer be withdrawn.");
        }

        var fromStatus = application.Status;
        application.Status = ApplicationStatus.Withdrawn;

        db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
        {
            RentalApplicationId = application.Id,
            FromStatus = fromStatus,
            ToStatus = ApplicationStatus.Withdrawn,
            ChangedByUserId = applicantUserId
        });

        await db.SaveChangesAsync();

        return ApplicationActionResult.Success();
    }

    public async Task<ApplicationActionResult> AddResidenceAsync(Guid applicationId, Guid applicantUserId, string address, string landlordName, string landlordPhone, DateOnly moveInDate, DateOnly moveOutDate)
    {
        if (moveOutDate < moveInDate)
        {
            return ApplicationActionResult.Failure("Move-out date must be on or after the move-in date.");
        }

        var application = await GetEditableOwnedAsync(applicationId, applicantUserId);
        if (application is null)
        {
            return ApplicationActionResult.Failure("This application can no longer be edited.");
        }

        db.Residences.Add(new Residence
        {
            RentalApplicationId = applicationId,
            Address = address,
            LandlordName = landlordName,
            LandlordPhone = landlordPhone,
            MoveInDate = moveInDate,
            MoveOutDate = moveOutDate
        });

        await db.SaveChangesAsync();

        return ApplicationActionResult.Success();
    }

    public async Task<ApplicationActionResult> UpdateResidenceAsync(Guid residenceId, Guid applicantUserId, string address, string landlordName, string landlordPhone, DateOnly moveInDate, DateOnly moveOutDate)
    {
        if (moveOutDate < moveInDate)
        {
            return ApplicationActionResult.Failure("Move-out date must be on or after the move-in date.");
        }

        var residence = await db.Residences.Include(r => r.RentalApplication).FirstOrDefaultAsync(r => r.Id == residenceId);
        if (residence is null || !IsEditableOwned(residence.RentalApplication, applicantUserId))
        {
            return ApplicationActionResult.Failure("This application can no longer be edited.");
        }

        residence.Address = address;
        residence.LandlordName = landlordName;
        residence.LandlordPhone = landlordPhone;
        residence.MoveInDate = moveInDate;
        residence.MoveOutDate = moveOutDate;

        await db.SaveChangesAsync();

        return ApplicationActionResult.Success();
    }

    public async Task<ApplicationActionResult> DeleteResidenceAsync(Guid residenceId, Guid applicantUserId)
    {
        var residence = await db.Residences.Include(r => r.RentalApplication).FirstOrDefaultAsync(r => r.Id == residenceId);
        if (residence is null || !IsEditableOwned(residence.RentalApplication, applicantUserId))
        {
            return ApplicationActionResult.Failure("This application can no longer be edited.");
        }

        db.Residences.Remove(residence);
        await db.SaveChangesAsync();

        return ApplicationActionResult.Success();
    }

    private async Task<RentalApplication?> GetEditableOwnedAsync(Guid id, Guid applicantUserId)
    {
        var application = await db.RentalApplications.FirstOrDefaultAsync(a => a.Id == id);
        return application is not null && IsEditableOwned(application, applicantUserId) ? application : null;
    }

    private static bool IsEditableOwned(RentalApplication application, Guid applicantUserId)
        => application.ApplicantUserId == applicantUserId
            && application.Status is ApplicationStatus.Draft or ApplicationStatus.Returned;
}
