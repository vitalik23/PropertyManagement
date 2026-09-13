using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Services;

namespace PropertyManagement.Tests.Services;

public class ApplicationServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly UserManager<User> _userManager;
    private readonly ApplicationService _service;

    public ApplicationServiceTests()
    {
        _userManager = TestUserManagerFactory.Create(_factory.Context);
        _service = new ApplicationService(_factory.Context, new UnitAvailabilityService(_factory.Context), _userManager);
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
        Assert.Equal("Please resolve all outstanding issues before submitting.", result.Error);
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

        var result = await _service.SaveApplicantInfoAsync(application.Id, intruder.Id, "Hacked Name", "555-9999", "hacked@test.com", "Nowhere", expectedVersion: 0);

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

    [Fact]
    public async Task SaveApplicantInfoAsync_WithInvalidEmail_StillPersistsTheAttemptedValue()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicant = await TestDataBuilder.CreateUserAsync(db);
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, applicant, ApplicationStatus.Draft, sectionsComplete: false);

        var result = await _service.SaveApplicantInfoAsync(application.Id, applicant.Id, "Jordan Applicant", "555-0100", "not-an-email", "42 Current St", expectedVersion: 0);

        Assert.True(result.Succeeded);
        var updated = await db.RentalApplications.FirstAsync(a => a.Id == application.Id);
        Assert.Equal("not-an-email", updated.Email);
    }

    [Fact]
    public async Task ValidateApplicantInfo_WithInvalidEmail_ReturnsFieldError()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicant = await TestDataBuilder.CreateUserAsync(db);
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, applicant, ApplicationStatus.Draft, sectionsComplete: false);
        application.Email = "not-an-email";

        var errors = _service.ValidateApplicantInfo(application);

        Assert.Contains(errors, e => e.Field == nameof(application.Email));
    }

    [Fact]
    public async Task ValidateResidenceHistory_WithNoResidences_ReturnsBlockingError()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicant = await TestDataBuilder.CreateUserAsync(db);
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, applicant, ApplicationStatus.Draft, sectionsComplete: false);

        var errors = _service.ValidateResidenceHistory(application);

        Assert.Single(errors);
    }

    [Fact]
    public async Task ValidateResidenceHistory_WithAtLeastOneResidence_ReturnsNoErrors()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicant = await TestDataBuilder.CreateUserAsync(db);
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, applicant, ApplicationStatus.Draft, sectionsComplete: false);
        await TestDataBuilder.CreateResidenceAsync(db, application);

        var reloaded = await db.RentalApplications.Include(a => a.Residences).FirstAsync(a => a.Id == application.Id);
        var errors = _service.ValidateResidenceHistory(reloaded);

        Assert.Empty(errors);
    }

    [Fact]
    public async Task SubmitAsync_MarkedCompletedButNowMissingResidences_StillRejected()
    {
        // ApplicantInfoCompletedAt/ResidenceHistoryCompletedAt are "visited" markers, not
        // validity guarantees once save-with-errors allows saving invalid/incomplete sections —
        // Submit must re-validate live state, not trust a stale completed timestamp.
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicant = await TestDataBuilder.CreateUserAsync(db);
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, applicant, ApplicationStatus.Draft, sectionsComplete: false);
        application.ApplicantInfoCompletedAt = DateTime.UtcNow;
        application.ResidenceHistoryCompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var result = await _service.SubmitAsync(application.Id, applicant.Id);

        Assert.False(result.Succeeded);
        Assert.Equal("Please resolve all outstanding issues before submitting.", result.Error);
    }

    [Fact]
    public async Task AddCoApplicantAsync_ValidApplicantEmail_AddsCoApplicant()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var primary = await TestDataBuilder.CreateApplicantUserAsync(db, _userManager, "primary1@test.com");
        var coApplicant = await TestDataBuilder.CreateApplicantUserAsync(db, _userManager, "co1@test.com");
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, primary, ApplicationStatus.Draft);

        var result = await _service.AddCoApplicantAsync(application.Id, primary.Id, "co1@test.com");

        Assert.True(result.Succeeded);
        var coApplicants = await db.ApplicationApplicants.Where(c => c.RentalApplicationId == application.Id).ToListAsync();
        Assert.Single(coApplicants);
        Assert.Equal(coApplicant.Id, coApplicants[0].UserId);
    }

    [Fact]
    public async Task AddCoApplicantAsync_UnknownEmail_Fails()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var primary = await TestDataBuilder.CreateApplicantUserAsync(db, _userManager, "primary2@test.com");
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, primary, ApplicationStatus.Draft);

        var result = await _service.AddCoApplicantAsync(application.Id, primary.Id, "doesnotexist@test.com");

        Assert.False(result.Succeeded);
        Assert.Empty(await db.ApplicationApplicants.Where(c => c.RentalApplicationId == application.Id).ToListAsync());
    }

    [Fact]
    public async Task AddCoApplicantAsync_AlreadyOnApplication_Fails()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var primary = await TestDataBuilder.CreateApplicantUserAsync(db, _userManager, "primary3@test.com");
        await TestDataBuilder.CreateApplicantUserAsync(db, _userManager, "co3@test.com");
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, primary, ApplicationStatus.Draft);
        await _service.AddCoApplicantAsync(application.Id, primary.Id, "co3@test.com");

        var result = await _service.AddCoApplicantAsync(application.Id, primary.Id, "co3@test.com");

        Assert.False(result.Succeeded);
        Assert.Single(await db.ApplicationApplicants.Where(c => c.RentalApplicationId == application.Id).ToListAsync());
    }

    [Fact]
    public async Task CoApplicant_CanSaveApplicantInfo_LikePrimaryApplicant()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var primary = await TestDataBuilder.CreateApplicantUserAsync(db, _userManager, "primary4@test.com");
        var coApplicant = await TestDataBuilder.CreateApplicantUserAsync(db, _userManager, "co4@test.com");
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, primary, ApplicationStatus.Draft);
        await _service.AddCoApplicantAsync(application.Id, primary.Id, "co4@test.com");

        var result = await _service.SaveApplicantInfoAsync(application.Id, coApplicant.Id, "Co Name", "555-1111", "co4@test.com", "1 Co St", expectedVersion: 0);

        Assert.True(result.Succeeded);
        var updated = await db.RentalApplications.FirstAsync(a => a.Id == application.Id);
        Assert.Equal("Co Name", updated.FullName);
    }

    [Fact]
    public async Task SaveApplicantInfoAsync_StaleVersion_RejectedWithoutOverwriting()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicant = await TestDataBuilder.CreateUserAsync(db);
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, applicant, ApplicationStatus.Draft);

        await _service.SaveApplicantInfoAsync(application.Id, applicant.Id, "First Save", "555-0001", "first@test.com", "1 First St", expectedVersion: 0);

        var result = await _service.SaveApplicantInfoAsync(application.Id, applicant.Id, "Second Save", "555-0002", "second@test.com", "2 Second St", expectedVersion: 0);

        Assert.False(result.Succeeded);
        var updated = await db.RentalApplications.FirstAsync(a => a.Id == application.Id);
        Assert.Equal("First Save", updated.FullName);
        Assert.Equal(1, updated.ApplicantInfoVersion);
    }

    [Fact]
    public async Task SaveApplicantInfoAsync_CorrectVersion_SucceedsAndIncrementsVersion()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicant = await TestDataBuilder.CreateUserAsync(db);
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, applicant, ApplicationStatus.Draft);

        await _service.SaveApplicantInfoAsync(application.Id, applicant.Id, "First Save", "555-0001", "first@test.com", "1 First St", expectedVersion: 0);
        var result = await _service.SaveApplicantInfoAsync(application.Id, applicant.Id, "Second Save", "555-0002", "second@test.com", "2 Second St", expectedVersion: 1);

        Assert.True(result.Succeeded);
        var updated = await db.RentalApplications.FirstAsync(a => a.Id == application.Id);
        Assert.Equal("Second Save", updated.FullName);
        Assert.Equal(2, updated.ApplicantInfoVersion);
    }

    [Fact]
    public async Task SavingApplicantInfo_DoesNotInterfereWith_ConcurrentResidenceHistoryConfirm()
    {
        // Direct check of the spec's "saves to different sections must not interfere" requirement:
        // both sections start at version 0 and are saved independently in the same "turn" — neither
        // should be rejected because of the other's write.
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicant = await TestDataBuilder.CreateUserAsync(db);
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, applicant, ApplicationStatus.Draft);

        var infoResult = await _service.SaveApplicantInfoAsync(application.Id, applicant.Id, "Name", "555-0000", "e@test.com", "Addr", expectedVersion: 0);
        var residenceResult = await _service.ConfirmResidenceHistoryAsync(application.Id, applicant.Id, expectedVersion: 0);

        Assert.True(infoResult.Succeeded);
        Assert.True(residenceResult.Succeeded);
    }

    [Fact]
    public async Task UpdateResidenceAsync_StaleVersion_Rejected()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var applicant = await TestDataBuilder.CreateUserAsync(db);
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, applicant, ApplicationStatus.Draft);
        var residence = await TestDataBuilder.CreateResidenceAsync(db, application);

        var moveIn = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2));
        var moveOut = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1));

        await _service.UpdateResidenceAsync(residence.Id, applicant.Id, "First Update", "LL", "555", moveIn, moveOut, expectedVersion: 0);
        var result = await _service.UpdateResidenceAsync(residence.Id, applicant.Id, "Second Update", "LL2", "555", moveIn, moveOut, expectedVersion: 0);

        Assert.False(result.Succeeded);
        var updated = await db.Residences.FirstAsync(r => r.Id == residence.Id);
        Assert.Equal("First Update", updated.Address);
    }
}
