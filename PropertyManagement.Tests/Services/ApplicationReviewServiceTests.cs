using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Services;

namespace PropertyManagement.Tests.Services;

public class ApplicationReviewServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly ApplicationReviewService _service;

    public ApplicationReviewServiceTests()
    {
        _service = new ApplicationReviewService(_factory.Context, new UnitAvailabilityService(_factory.Context));
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task ReviewAsync_Approve_OnAvailableUnit_CreatesLeaseAndApprovesApplication()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicant = await TestDataBuilder.CreateUserAsync(db);
        var reviewer = await TestDataBuilder.CreateUserAsync(db, "pm@test.com");
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, applicant, ApplicationStatus.Submitted, sectionsComplete: true);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var result = await _service.ReviewAsync(application.Id, ReviewOutcome.Approve, null, reviewer.Id);

        Assert.True(result.Succeeded);

        var updated = await db.RentalApplications.FirstAsync(a => a.Id == application.Id);
        Assert.Equal(ApplicationStatus.Approved, updated.Status);

        var lease = await db.Leases.SingleAsync(l => l.RentalApplicationId == application.Id);
        Assert.Equal(today, lease.StartDate);
        Assert.Equal(today.AddMonths(12), lease.EndDate);
    }

    [Fact]
    public async Task ReviewAsync_Approve_WhenUnitAlreadyLeased_FailsWithoutCreatingSecondLease()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicant = await TestDataBuilder.CreateUserAsync(db);
        var reviewer = await TestDataBuilder.CreateUserAsync(db, "pm@test.com");

        var leasedApplication = await TestDataBuilder.CreateApplicationAsync(db, unit, applicant, ApplicationStatus.Approved, sectionsComplete: true);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await TestDataBuilder.CreateLeaseAsync(db, unit, leasedApplication, today.AddDays(-1), today.AddMonths(11));

        var secondApplicant = await TestDataBuilder.CreateUserAsync(db, "second@test.com");
        var secondApplication = await TestDataBuilder.CreateApplicationAsync(db, unit, secondApplicant, ApplicationStatus.Submitted, sectionsComplete: true);

        var result = await _service.ReviewAsync(secondApplication.Id, ReviewOutcome.Approve, null, reviewer.Id);

        Assert.False(result.Succeeded);
        Assert.Equal("This unit already has an active lease.", result.Error);

        var updated = await db.RentalApplications.FirstAsync(a => a.Id == secondApplication.Id);
        Assert.Equal(ApplicationStatus.Submitted, updated.Status);
        Assert.Equal(1, await db.Leases.CountAsync(l => l.UnitId == unit.Id));
    }

    [Fact]
    public async Task ReviewAsync_NonSubmittedApplication_Fails()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicant = await TestDataBuilder.CreateUserAsync(db);
        var reviewer = await TestDataBuilder.CreateUserAsync(db, "pm@test.com");
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, applicant, ApplicationStatus.Draft);

        var result = await _service.ReviewAsync(application.Id, ReviewOutcome.Approve, null, reviewer.Id);

        Assert.False(result.Succeeded);
        Assert.Equal("Only submitted applications can be reviewed.", result.Error);
    }

    [Fact]
    public async Task ReviewAsync_Return_WithoutComment_Fails()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicant = await TestDataBuilder.CreateUserAsync(db);
        var reviewer = await TestDataBuilder.CreateUserAsync(db, "pm@test.com");
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, applicant, ApplicationStatus.Submitted, sectionsComplete: true);

        var result = await _service.ReviewAsync(application.Id, ReviewOutcome.Return, null, reviewer.Id);

        Assert.False(result.Succeeded);
        var updated = await db.RentalApplications.FirstAsync(a => a.Id == application.Id);
        Assert.Equal(ApplicationStatus.Submitted, updated.Status);
    }

    [Fact]
    public async Task ReviewAsync_Deny_WithoutComment_Fails()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicant = await TestDataBuilder.CreateUserAsync(db);
        var reviewer = await TestDataBuilder.CreateUserAsync(db, "pm@test.com");
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, applicant, ApplicationStatus.Submitted, sectionsComplete: true);

        var result = await _service.ReviewAsync(application.Id, ReviewOutcome.Deny, "   ", reviewer.Id);

        Assert.False(result.Succeeded);
        var updated = await db.RentalApplications.FirstAsync(a => a.Id == application.Id);
        Assert.Equal(ApplicationStatus.Submitted, updated.Status);
    }

    [Fact]
    public async Task ReviewAsync_Return_WithComment_SetsStatusAndWritesHistory()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicant = await TestDataBuilder.CreateUserAsync(db);
        var reviewer = await TestDataBuilder.CreateUserAsync(db, "pm@test.com");
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, applicant, ApplicationStatus.Submitted, sectionsComplete: true);

        var result = await _service.ReviewAsync(application.Id, ReviewOutcome.Return, "Please clarify income.", reviewer.Id);

        Assert.True(result.Succeeded);
        var updated = await db.RentalApplications.FirstAsync(a => a.Id == application.Id);
        Assert.Equal(ApplicationStatus.Returned, updated.Status);

        var history = await db.ApplicationStatusHistories.SingleAsync(h => h.RentalApplicationId == application.Id);
        Assert.Equal(ApplicationStatus.Submitted, history.FromStatus);
        Assert.Equal(ApplicationStatus.Returned, history.ToStatus);
        Assert.Equal("Please clarify income.", history.Comment);
        Assert.Equal(reviewer.Id, history.ChangedByUserId);
    }

    [Fact]
    public async Task ReviewAsync_Deny_WithComment_SetsStatusAndWritesHistory()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicant = await TestDataBuilder.CreateUserAsync(db);
        var reviewer = await TestDataBuilder.CreateUserAsync(db, "pm@test.com");
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, applicant, ApplicationStatus.Submitted, sectionsComplete: true);

        var result = await _service.ReviewAsync(application.Id, ReviewOutcome.Deny, "Insufficient income.", reviewer.Id);

        Assert.True(result.Succeeded);
        var updated = await db.RentalApplications.FirstAsync(a => a.Id == application.Id);
        Assert.Equal(ApplicationStatus.Denied, updated.Status);

        var history = await db.ApplicationStatusHistories.SingleAsync(h => h.RentalApplicationId == application.Id);
        Assert.Equal(ApplicationStatus.Denied, history.ToStatus);
        Assert.Equal("Insufficient income.", history.Comment);
    }
}
