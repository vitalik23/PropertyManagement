using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Constants;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Domain.Validation;
using PropertyManagement.Infrastructure.Data;

namespace PropertyManagement.Infrastructure.Services;

public class ApplicationService(ApplicationDbContext db, IUnitAvailabilityService availabilityService, UserManager<User> userManager) : IApplicationService
{
    public async Task<RentalApplication> GetOrStartAsync(Guid unitId, Guid applicantUserId)
    {
        var existing = await db.RentalApplications
            .Include(a => a.CoApplicants)
            .FirstOrDefaultAsync(a =>
                a.UnitId == unitId &&
                (a.ApplicantUserId == applicantUserId || a.CoApplicants.Any(c => c.UserId == applicantUserId)) &&
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
            .Include(a => a.CoApplicants).ThenInclude(c => c.User)
            .FirstOrDefaultAsync(a => a.Id == id);

    public Task<List<RentalApplication>> GetMyApplicationsAsync(Guid applicantUserId)
        => db.RentalApplications
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Where(a => a.ApplicantUserId == applicantUserId || a.CoApplicants.Any(c => c.UserId == applicantUserId))
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

    public Task<List<RentalApplication>> GetFilteredApplicationsAsync(Guid? applicantUserId, ApplicationStatus? status, Guid? propertyId)
        => BuildFilteredQuery(applicantUserId, status, propertyId).OrderByDescending(a => a.CreatedAt).ToListAsync();

    public async Task<PagedResult<RentalApplication>> GetFilteredApplicationsPageAsync(Guid? applicantUserId, ApplicationStatus? status, Guid? propertyId, string? sortBy, bool sortDescending, int page, int pageSize)
    {
        var query = BuildFilteredQuery(applicantUserId, status, propertyId);

        var total = await query.CountAsync();

        query = sortBy switch
        {
            "status" => sortDescending ? query.OrderByDescending(a => a.Status) : query.OrderBy(a => a.Status),
            "property" => sortDescending ? query.OrderByDescending(a => a.Unit.Property.Name) : query.OrderBy(a => a.Unit.Property.Name),
            "unit" => sortDescending ? query.OrderByDescending(a => a.Unit.UnitNumber) : query.OrderBy(a => a.Unit.UnitNumber),
            "applicant" => sortDescending ? query.OrderByDescending(a => a.FullName) : query.OrderBy(a => a.FullName),
            _ => sortDescending ? query.OrderByDescending(a => a.CreatedAt) : query.OrderBy(a => a.CreatedAt)
        };

        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PagedResult<RentalApplication>(rows, total);
    }

    private IQueryable<RentalApplication> BuildFilteredQuery(Guid? applicantUserId, ApplicationStatus? status, Guid? propertyId)
    {
        var query = db.RentalApplications
            .Include(a => a.Unit).ThenInclude(u => u.Property)
            .Include(a => a.ApplicantUser)
            .AsQueryable();

        if (applicantUserId is not null)
        {
            query = query.Where(a => a.ApplicantUserId == applicantUserId || a.CoApplicants.Any(c => c.UserId == applicantUserId));
        }

        if (status is not null)
        {
            query = query.Where(a => a.Status == status);
        }

        if (propertyId is not null)
        {
            query = query.Where(a => a.Unit.PropertyId == propertyId);
        }

        return query;
    }

    public Task<List<ApplicationStatusHistory>> GetStatusHistoryAsync(Guid applicationId)
        => db.ApplicationStatusHistories
            .Include(h => h.ChangedByUser)
            .Where(h => h.RentalApplicationId == applicationId)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync();

    public Task<Residence?> GetResidenceAsync(Guid residenceId)
        => db.Residences
            .Include(r => r.RentalApplication).ThenInclude(a => a.CoApplicants)
            .FirstOrDefaultAsync(r => r.Id == residenceId);

    public List<SectionValidationError> ValidateApplicantInfo(RentalApplication application)
    {
        var target = new ApplicantInfoValidationTarget
        {
            FullName = application.FullName,
            PhoneNumber = application.PhoneNumber,
            Email = application.Email,
            CurrentAddress = application.CurrentAddress
        };

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(target, new ValidationContext(target), results, validateAllProperties: true);

        return results
            .Select(r => new SectionValidationError("Applicant Information", r.MemberNames.FirstOrDefault() ?? string.Empty, r.ErrorMessage ?? "Invalid value."))
            .ToList();
    }

    public List<SectionValidationError> ValidateResidenceHistory(RentalApplication application)
        => application.Residences.Count == 0
            ? [new SectionValidationError("Residence History", nameof(RentalApplication.Residences), "At least one residence is required.")]
            : [];

    public async Task<ApplicationActionResult> SaveApplicantInfoAsync(Guid id, Guid applicantUserId, string fullName, string phoneNumber, string email, string currentAddress, int expectedVersion)
    {
        var application = await GetEditableOwnedAsync(id, applicantUserId);
        if (application is null)
        {
            return ApplicationActionResult.Failure("This application can no longer be edited.");
        }

        if (application.ApplicantInfoVersion != expectedVersion)
        {
            return ApplicationActionResult.Failure("This section was changed by another applicant. Reload to see the latest version.");
        }

        // ASP.NET Core's default model binding converts an empty posted form field to null (not
        // "") for string properties, but these columns are NOT NULL — coalesce so a genuinely
        // blank field still saves (save-with-errors) instead of crashing on constraint violation.
        application.FullName = fullName ?? string.Empty;
        application.PhoneNumber = phoneNumber ?? string.Empty;
        application.Email = email ?? string.Empty;
        application.CurrentAddress = currentAddress ?? string.Empty;
        application.ApplicantInfoCompletedAt = DateTime.UtcNow;
        application.ApplicantInfoVersion++;

        await db.SaveChangesAsync();

        return ApplicationActionResult.Success();
    }

    public async Task<ApplicationActionResult> ConfirmResidenceHistoryAsync(Guid id, Guid applicantUserId, int expectedVersion)
    {
        var application = await GetEditableOwnedAsync(id, applicantUserId);
        if (application is null)
        {
            return ApplicationActionResult.Failure("This application can no longer be edited.");
        }

        if (application.ResidenceHistoryVersion != expectedVersion)
        {
            return ApplicationActionResult.Failure("This section was changed by another applicant. Reload to see the latest version.");
        }

        application.ResidenceHistoryCompletedAt = DateTime.UtcNow;
        application.ResidenceHistoryVersion++;
        await db.SaveChangesAsync();

        return ApplicationActionResult.Success();
    }

    public async Task<ApplicationActionResult> SubmitAsync(Guid id, Guid applicantUserId)
    {
        var application = await db.RentalApplications
            .Include(a => a.Unit)
            .Include(a => a.Residences)
            .Include(a => a.CoApplicants)
            .FirstOrDefaultAsync(a => a.Id == id && (a.ApplicantUserId == applicantUserId || a.CoApplicants.Any(c => c.UserId == applicantUserId)));

        if (application is null)
        {
            return ApplicationActionResult.Failure("Application not found.");
        }

        if (application.Status is not (ApplicationStatus.Draft or ApplicationStatus.Returned))
        {
            return ApplicationActionResult.Failure("This application can no longer be edited.");
        }

        if (ValidateApplicantInfo(application).Count > 0 || ValidateResidenceHistory(application).Count > 0)
        {
            return ApplicationActionResult.Failure("Please resolve all outstanding issues before submitting.");
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
        var application = await db.RentalApplications
            .Include(a => a.CoApplicants)
            .FirstOrDefaultAsync(a => a.Id == id && (a.ApplicantUserId == applicantUserId || a.CoApplicants.Any(c => c.UserId == applicantUserId)));
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

    public async Task<ApplicationActionResult> UpdateResidenceAsync(Guid residenceId, Guid applicantUserId, string address, string landlordName, string landlordPhone, DateOnly moveInDate, DateOnly moveOutDate, int expectedVersion)
    {
        if (moveOutDate < moveInDate)
        {
            return ApplicationActionResult.Failure("Move-out date must be on or after the move-in date.");
        }

        var residence = await db.Residences
            .Include(r => r.RentalApplication).ThenInclude(a => a.CoApplicants)
            .FirstOrDefaultAsync(r => r.Id == residenceId);
        if (residence is null || !IsEditableOwned(residence.RentalApplication, applicantUserId))
        {
            return ApplicationActionResult.Failure("This application can no longer be edited.");
        }

        if (residence.Version != expectedVersion)
        {
            return ApplicationActionResult.Failure("This residence was changed by another applicant. Reload to see the latest version.");
        }

        residence.Address = address;
        residence.LandlordName = landlordName;
        residence.LandlordPhone = landlordPhone;
        residence.MoveInDate = moveInDate;
        residence.MoveOutDate = moveOutDate;
        residence.Version++;

        await db.SaveChangesAsync();

        return ApplicationActionResult.Success();
    }

    public async Task<ApplicationActionResult> DeleteResidenceAsync(Guid residenceId, Guid applicantUserId, int expectedVersion)
    {
        var residence = await db.Residences
            .Include(r => r.RentalApplication).ThenInclude(a => a.CoApplicants)
            .FirstOrDefaultAsync(r => r.Id == residenceId);
        if (residence is null || !IsEditableOwned(residence.RentalApplication, applicantUserId))
        {
            return ApplicationActionResult.Failure("This application can no longer be edited.");
        }

        if (residence.Version != expectedVersion)
        {
            return ApplicationActionResult.Failure("This residence was changed by another applicant. Reload to see the latest version.");
        }

        db.Residences.Remove(residence);
        await db.SaveChangesAsync();

        return ApplicationActionResult.Success();
    }

    public async Task<ApplicationActionResult> AddCoApplicantAsync(Guid applicationId, Guid requestingUserId, string coApplicantEmail)
    {
        var application = await GetEditableOwnedAsync(applicationId, requestingUserId);
        if (application is null)
        {
            return ApplicationActionResult.Failure("This application can no longer be edited.");
        }

        var candidate = await userManager.FindByEmailAsync(coApplicantEmail);
        if (candidate is null || !await userManager.IsInRoleAsync(candidate, Roles.Applicant))
        {
            return ApplicationActionResult.Failure("No applicant account was found with that email.");
        }

        if (candidate.Id == application.ApplicantUserId || application.CoApplicants.Any(c => c.UserId == candidate.Id))
        {
            return ApplicationActionResult.Failure("That person is already on this application.");
        }

        db.ApplicationApplicants.Add(new ApplicationApplicant
        {
            RentalApplicationId = application.Id,
            UserId = candidate.Id
        });

        await db.SaveChangesAsync();

        return ApplicationActionResult.Success();
    }

    public Task<List<ApplicationApplicant>> GetCoApplicantsAsync(Guid applicationId)
        => db.ApplicationApplicants
            .Include(c => c.User)
            .Where(c => c.RentalApplicationId == applicationId)
            .ToListAsync();

    private async Task<RentalApplication?> GetEditableOwnedAsync(Guid id, Guid applicantUserId)
    {
        var application = await db.RentalApplications.Include(a => a.CoApplicants).FirstOrDefaultAsync(a => a.Id == id);
        return application is not null && IsEditableOwned(application, applicantUserId) ? application : null;
    }

    private static bool IsEditableOwned(RentalApplication application, Guid applicantUserId)
        => (application.ApplicantUserId == applicantUserId || application.CoApplicants.Any(c => c.UserId == applicantUserId))
            && application.Status is ApplicationStatus.Draft or ApplicationStatus.Returned;
}
