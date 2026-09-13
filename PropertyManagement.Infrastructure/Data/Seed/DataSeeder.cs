using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Services;
using PropertyManagement.Domain.Constants;
using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Infrastructure.Data.Seed;

public class DataSeeder(
    ApplicationDbContext db,
    UserManager<User> userManager,
    IApplicationService applicationService,
    IApplicationReviewService reviewService) : IDataSeeder
{
    public const string SeedPassword = "Passw0rd!1";

    private readonly Faker _faker = new();

    public async Task SeedAsync()
    {
        if (await db.Properties.AnyAsync())
        {
            return;
        }

        Randomizer.Seed = new Random(8675309);

        var unitTypes = await SeedUnitTypesAsync();
        var (propertyManagers, applicants) = await SeedUsersAsync();
        var units = await SeedPropertiesAndUnitsAsync(unitTypes);

        // Retire the last unit type only after a unit already uses it, so the
        // "still displays on units that use it, not selectable elsewhere" rule
        // has something concrete to demonstrate out of the box.
        unitTypes[^1].IsActive = false;
        await db.SaveChangesAsync();

        await SeedApplicationsAsync(units, applicants, propertyManagers[0]);
    }

    private async Task<List<UnitType>> SeedUnitTypesAsync()
    {
        var names = new[] { "Studio", "One Bedroom", "Two Bedroom", "Three Bedroom", "Legacy Loft" };
        var unitTypes = names.Select(name => new UnitType { Name = name, IsActive = true }).ToList();

        db.UnitTypes.AddRange(unitTypes);
        await db.SaveChangesAsync();

        return unitTypes;
    }

    private async Task<(List<User> PropertyManagers, List<User> Applicants)> SeedUsersAsync()
    {
        var propertyManagers = new List<User>();
        for (var i = 1; i <= 2; i++)
        {
            var user = new User { UserName = $"pm{i}@example.com", Email = $"pm{i}@example.com", EmailConfirmed = true };
            await userManager.CreateAsync(user, SeedPassword);
            await userManager.AddToRoleAsync(user, Roles.PropertyManager);
            propertyManagers.Add(user);
        }

        var applicantFaker = new Faker<User>()
            .RuleFor(u => u.Email, f => f.Internet.Email())
            .RuleFor(u => u.UserName, (_, u) => u.Email)
            .RuleFor(u => u.EmailConfirmed, _ => true);

        var applicants = new List<User>();
        foreach (var user in applicantFaker.Generate(6))
        {
            await userManager.CreateAsync(user, SeedPassword);
            await userManager.AddToRoleAsync(user, Roles.Applicant);
            applicants.Add(user);
        }

        return (propertyManagers, applicants);
    }

    private async Task<List<Unit>> SeedPropertiesAndUnitsAsync(List<UnitType> unitTypes)
    {
        var activeTypesForNewUnits = unitTypes.Take(unitTypes.Count - 1).ToArray();
        var retiringType = unitTypes[^1];

        var propertyFaker = new Faker<Property>()
            .RuleFor(p => p.Name, f => $"{f.Address.City()} {f.PickRandom("Apartments", "Residences", "Commons", "Flats")}")
            .RuleFor(p => p.AddressLine1, f => f.Address.StreetAddress())
            .RuleFor(p => p.City, f => f.Address.City())
            .RuleFor(p => p.State, f => f.Address.StateAbbr())
            .RuleFor(p => p.ZipCode, f => f.Address.ZipCode());

        var properties = propertyFaker.Generate(3);
        db.Properties.AddRange(properties);
        await db.SaveChangesAsync();

        var units = new List<Unit>();

        for (var p = 0; p < properties.Count; p++)
        {
            for (var u = 1; u <= 3; u++)
            {
                var isRetiringTypeUnit = p == 0 && u == 1;
                var unitType = isRetiringTypeUnit ? retiringType : _faker.PickRandom(activeTypesForNewUnits);

                units.Add(new Unit
                {
                    PropertyId = properties[p].Id,
                    UnitNumber = $"{p + 1}0{u}",
                    Bedrooms = _faker.Random.Int(0, 3),
                    MonthlyRent = _faker.Random.Decimal(900, 3200),
                    UnitTypeId = unitType.Id
                });
            }
        }

        db.Units.AddRange(units);
        await db.SaveChangesAsync();

        return units;
    }

    private async Task SeedApplicationsAsync(List<Unit> units, List<User> applicants, User reviewer)
    {
        // Draft — just started, no sections completed yet.
        await applicationService.GetOrStartAsync(units[0].Id, applicants[0].Id);

        // Submitted — both sections completed and submitted.
        await SubmitFullApplicationAsync(units[1].Id, applicants[1].Id);

        // Returned — submitted, then sent back with a comment.
        var returnedApp = await SubmitFullApplicationAsync(units[2].Id, applicants[2].Id);
        await reviewService.ReviewAsync(returnedApp.Id, ReviewOutcome.Return, "Please add more residence history detail.", reviewer.Id);

        // Denied — submitted, then denied with a comment.
        var deniedApp = await SubmitFullApplicationAsync(units[3].Id, applicants[3].Id);
        await reviewService.ReviewAsync(deniedApp.Id, ReviewOutcome.Deny, "Insufficient income documentation.", reviewer.Id);

        // Approved — submitted, then approved, issuing a real 12-month lease covering today.
        var approvedApp = await SubmitFullApplicationAsync(units[4].Id, applicants[4].Id);
        await reviewService.ReviewAsync(approvedApp.Id, ReviewOutcome.Approve, null, reviewer.Id);

        // Withdrawn — started, then withdrawn by the applicant.
        var withdrawnApp = await applicationService.GetOrStartAsync(units[5].Id, applicants[5].Id);
        await applicationService.WithdrawAsync(withdrawnApp.Id, applicants[5].Id);
    }

    private async Task<RentalApplication> SubmitFullApplicationAsync(Guid unitId, Guid applicantId)
    {
        var application = await applicationService.GetOrStartAsync(unitId, applicantId);

        await applicationService.SaveApplicantInfoAsync(
            application.Id, applicantId,
            fullName: _faker.Name.FullName(),
            phoneNumber: _faker.Phone.PhoneNumber("###-###-####"),
            email: _faker.Internet.Email(),
            currentAddress: _faker.Address.FullAddress(),
            expectedVersion: 0);

        await applicationService.AddResidenceAsync(
            application.Id, applicantId,
            address: _faker.Address.FullAddress(),
            landlordName: _faker.Name.FullName(),
            landlordPhone: _faker.Phone.PhoneNumber("###-###-####"),
            moveInDate: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-3)),
            moveOutDate: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)));

        await applicationService.ConfirmResidenceHistoryAsync(application.Id, applicantId, expectedVersion: 0);
        await applicationService.SubmitAsync(application.Id, applicantId);

        return application;
    }
}
