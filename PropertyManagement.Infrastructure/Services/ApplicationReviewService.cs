using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Data;

namespace PropertyManagement.Infrastructure.Services;

public class ApplicationReviewService(ApplicationDbContext db, IUnitAvailabilityService availabilityService) : IApplicationReviewService
{
    public async Task<ApplicationActionResult> ReviewAsync(Guid applicationId, ReviewOutcome outcome, string? comment, Guid reviewerId)
    {
        var application = await db.RentalApplications
            .Include(a => a.Unit)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (application is null)
        {
            return ApplicationActionResult.Failure("Application not found.");
        }

        if (application.Status != ApplicationStatus.Submitted)
        {
            return ApplicationActionResult.Failure("Only submitted applications can be reviewed.");
        }

        if (outcome is ReviewOutcome.Return or ReviewOutcome.Deny && string.IsNullOrWhiteSpace(comment))
        {
            return ApplicationActionResult.Failure("A comment is required for Return or Deny.");
        }

        var fromStatus = application.Status;

        switch (outcome)
        {
            case ReviewOutcome.Approve:
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                if (!await availabilityService.IsAvailableAsync(application.UnitId, today))
                {
                    return ApplicationActionResult.Failure("This unit already has an active lease.");
                }

                db.Leases.Add(new Lease
                {
                    UnitId = application.UnitId,
                    RentalApplicationId = application.Id,
                    StartDate = today,
                    EndDate = today.AddMonths(12)
                });

                application.Status = ApplicationStatus.Approved;
                break;

            case ReviewOutcome.Return:
                application.Status = ApplicationStatus.Returned;
                break;

            case ReviewOutcome.Deny:
                application.Status = ApplicationStatus.Denied;
                break;
        }

        db.ApplicationStatusHistories.Add(new ApplicationStatusHistory
        {
            RentalApplicationId = application.Id,
            FromStatus = fromStatus,
            ToStatus = application.Status,
            ChangedByUserId = reviewerId,
            Comment = comment
        });

        await db.SaveChangesAsync();

        return ApplicationActionResult.Success();
    }
}
