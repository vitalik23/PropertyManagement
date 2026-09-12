using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Services;

namespace PropertyManagement.Tests.Services;

public class ApplicationServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly ApplicationService _service;

    public ApplicationServiceTests()
    {
        _service = new ApplicationService(_factory.Context, new UnitAvailabilityService(_factory.Context));
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task SubmitAsync_DraftWithCompletedSectionsAndAvailableUnit_Succeeds()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicant = await TestDataBuilder.CreateUserAsync(db);
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, applicant, ApplicationStatus.Draft, sectionsComplete: true);

        var result = await _service.SubmitAsync(application.Id, applicant.Id);

        Assert.True(result.Succeeded);
        var updated = await db.RentalApplications.FirstAsync(a => a.Id == application.Id);
        Assert.Equal(ApplicationStatus.Submitted, updated.Status);

        var history = await db.ApplicationStatusHistories.SingleAsync(h => h.RentalApplicationId == application.Id);
        Assert.Equal(ApplicationStatus.Draft, history.FromStatus);
        Assert.Equal(ApplicationStatus.Submitted, history.ToStatus);
    }

    [Fact]
    public async Task SubmitAsync_IncompleteSections_Fails()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicant = await TestDataBuilder.CreateUserAsync(db);
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, applicant, ApplicationStatus.Draft, sectionsComplete: false);

        var result = await _service.SubmitAsync(application.Id, applicant.Id);

        Assert.False(result.Succeeded);
        Assert.Equal("Please complete both sections before submitting.", result.Error);
        var updated = await db.RentalApplications.FirstAsync(a => a.Id == application.Id);
        Assert.Equal(ApplicationStatus.Draft, updated.Status);
    }

    [Fact]
    public async Task SubmitAsync_UnitAlreadyLeased_Fails()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicant = await TestDataBuilder.CreateUserAsync(db);

        var otherApplicant = await TestDataBuilder.CreateUserAsync(db, "other@test.com");
        var leasedApplication = await TestDataBuilder.CreateApplicationAsync(db, unit, otherApplicant, ApplicationStatus.Approved, sectionsComplete: true);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await TestDataBuilder.CreateLeaseAsync(db, unit, leasedApplication, today.AddDays(-1), today.AddMonths(11));

        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, applicant, ApplicationStatus.Draft, sectionsComplete: true);

        var result = await _service.SubmitAsync(application.Id, applicant.Id);

        Assert.False(result.Succeeded);
        Assert.Equal("This unit is no longer available.", result.Error);
        var updated = await db.RentalApplications.FirstAsync(a => a.Id == application.Id);
        Assert.Equal(ApplicationStatus.Draft, updated.Status);
    }

    [Fact]
    public async Task SubmitAsync_ApplicationAlreadySubmitted_Fails()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicant = await TestDataBuilder.CreateUserAsync(db);
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, applicant, ApplicationStatus.Submitted, sectionsComplete: true);

        var result = await _service.SubmitAsync(application.Id, applicant.Id);

        Assert.False(result.Succeeded);
        Assert.Equal("This application can no longer be edited.", result.Error);
    }

    [Theory]
    [InlineData(ApplicationStatus.Draft)]
    [InlineData(ApplicationStatus.Submitted)]
    [InlineData(ApplicationStatus.Returned)]
    public async Task WithdrawAsync_FromNonTerminalStatus_Succeeds(ApplicationStatus status)
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicant = await TestDataBuilder.CreateUserAsync(db);
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, applicant, status, sectionsComplete: true);

        var result = await _service.WithdrawAsync(application.Id, applicant.Id);

        Assert.True(result.Succeeded);
        var updated = await db.RentalApplications.FirstAsync(a => a.Id == application.Id);
        Assert.Equal(ApplicationStatus.Withdrawn, updated.Status);
    }

    [Theory]
    [InlineData(ApplicationStatus.Approved)]
    [InlineData(ApplicationStatus.Denied)]
    [InlineData(ApplicationStatus.Withdrawn)]
    public async Task WithdrawAsync_FromTerminalStatus_Fails(ApplicationStatus status)
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicant = await TestDataBuilder.CreateUserAsync(db);
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, applicant, status, sectionsComplete: true);

        var result = await _service.WithdrawAsync(application.Id, applicant.Id);

        Assert.False(result.Succeeded);
        var updated = await db.RentalApplications.FirstAsync(a => a.Id == application.Id);
        Assert.Equal(status, updated.Status);
    }

    [Fact]
    public async Task SaveApplicantInfoAsync_WrongOwner_Fails()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var owner = await TestDataBuilder.CreateUserAsync(db);
        var intruder = await TestDataBuilder.CreateUserAsync(db, "intruder@test.com");
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, owner, ApplicationStatus.Draft);

        var result = await _service.SaveApplicantInfoAsync(application.Id, intruder.Id, "Hacked Name", "555-9999", "hacked@test.com", "Nowhere");

        Assert.False(result.Succeeded);
        var updated = await db.RentalApplications.FirstAsync(a => a.Id == application.Id);
        Assert.Equal("Test Applicant", updated.FullName);
    }

    [Fact]
    public async Task AddResidenceAsync_WrongOwner_Fails()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var owner = await TestDataBuilder.CreateUserAsync(db);
        var intruder = await TestDataBuilder.CreateUserAsync(db, "intruder@test.com");
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, owner, ApplicationStatus.Draft);

        var result = await _service.AddResidenceAsync(
            application.Id, intruder.Id, "1 Hack St", "Landlord", "555-0000",
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)), DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)));

        Assert.False(result.Succeeded);
        Assert.Empty(await db.Residences.Where(r => r.RentalApplicationId == application.Id).ToListAsync());
    }

    [Fact]
    public async Task GetFilteredApplicationsAsync_NoFilters_ReturnsAllApplications()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicantA = await TestDataBuilder.CreateUserAsync(db, "a@test.com");
        var applicantB = await TestDataBuilder.CreateUserAsync(db, "b@test.com");
        await TestDataBuilder.CreateApplicationAsync(db, unit, applicantA, ApplicationStatus.Draft);
        await TestDataBuilder.CreateApplicationAsync(db, unit, applicantB, ApplicationStatus.Submitted);

        var results = await _service.GetFilteredApplicationsAsync(null, null, null);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task GetFilteredApplicationsAsync_FiltersByStatus()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicantA = await TestDataBuilder.CreateUserAsync(db, "a@test.com");
        var applicantB = await TestDataBuilder.CreateUserAsync(db, "b@test.com");
        await TestDataBuilder.CreateApplicationAsync(db, unit, applicantA, ApplicationStatus.Draft);
        var submitted = await TestDataBuilder.CreateApplicationAsync(db, unit, applicantB, ApplicationStatus.Submitted);

        var results = await _service.GetFilteredApplicationsAsync(null, ApplicationStatus.Submitted, null);

        Assert.Single(results);
        Assert.Equal(submitted.Id, results[0].Id);
    }

    [Fact]
    public async Task GetFilteredApplicationsAsync_FiltersByProperty()
    {
        var db = _factory.Context;
        var propertyA = await TestDataBuilder.CreatePropertyAsync(db);
        var propertyB = await TestDataBuilder.CreatePropertyAsync(db);
        var unitType = await TestDataBuilder.CreateUnitTypeAsync(db);
        var unitA = await TestDataBuilder.CreateUnitAsync(db, propertyA, unitType);
        var unitB = await TestDataBuilder.CreateUnitAsync(db, propertyB, unitType);
        var applicant = await TestDataBuilder.CreateUserAsync(db);
        var applicationA = await TestDataBuilder.CreateApplicationAsync(db, unitA, applicant, ApplicationStatus.Draft);
        await TestDataBuilder.CreateApplicationAsync(db, unitB, applicant, ApplicationStatus.Draft);

        var results = await _service.GetFilteredApplicationsAsync(null, null, propertyA.Id);

        Assert.Single(results);
        Assert.Equal(applicationA.Id, results[0].Id);
    }

    [Fact]
    public async Task GetFilteredApplicationsAsync_FiltersByApplicant()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicantA = await TestDataBuilder.CreateUserAsync(db, "a@test.com");
        var applicantB = await TestDataBuilder.CreateUserAsync(db, "b@test.com");
        var applicationA = await TestDataBuilder.CreateApplicationAsync(db, unit, applicantA, ApplicationStatus.Draft);
        await TestDataBuilder.CreateApplicationAsync(db, unit, applicantB, ApplicationStatus.Draft);

        var results = await _service.GetFilteredApplicationsAsync(applicantA.Id, null, null);

        Assert.Single(results);
        Assert.Equal(applicationA.Id, results[0].Id);
    }

    [Fact]
    public async Task GetFilteredApplicationsAsync_CombinedFilters_Narrow()
    {
        var db = _factory.Context;
        var property = await TestDataBuilder.CreatePropertyAsync(db);
        var unitType = await TestDataBuilder.CreateUnitTypeAsync(db);
        var unit = await TestDataBuilder.CreateUnitAsync(db, property, unitType);
        var applicant = await TestDataBuilder.CreateUserAsync(db);
        var matching = await TestDataBuilder.CreateApplicationAsync(db, unit, applicant, ApplicationStatus.Submitted);
        await TestDataBuilder.CreateApplicationAsync(db, unit, applicant, ApplicationStatus.Draft);

        var results = await _service.GetFilteredApplicationsAsync(applicant.Id, ApplicationStatus.Submitted, property.Id);

        Assert.Single(results);
        Assert.Equal(matching.Id, results[0].Id);
    }
}
