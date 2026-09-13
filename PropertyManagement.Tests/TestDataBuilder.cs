using Microsoft.AspNetCore.Identity;
using PropertyManagement.Domain.Constants;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Domain.Enums;
using PropertyManagement.Infrastructure.Data;

namespace PropertyManagement.Tests;

public static class TestDataBuilder
{
    public static async Task<Property> CreatePropertyAsync(ApplicationDbContext db)
    {
        var property = new Property
        {
            Name = "Test Property",
            AddressLine1 = "123 Main St",
            City = "Testville",
            State = "TS",
            ZipCode = "00000"
        };

        db.Properties.Add(property);
        await db.SaveChangesAsync();
        return property;
    }

    public static async Task<UnitType> CreateUnitTypeAsync(ApplicationDbContext db, bool isActive = true)
    {
        var unitType = new UnitType { Name = "Standard", IsActive = isActive };
        db.UnitTypes.Add(unitType);
        await db.SaveChangesAsync();
        return unitType;
    }

    public static async Task<Unit> CreateUnitAsync(ApplicationDbContext db, Property? property = null, UnitType? unitType = null)
    {
        property ??= await CreatePropertyAsync(db);
        unitType ??= await CreateUnitTypeAsync(db);

        var unit = new Unit
        {
            PropertyId = property.Id,
            UnitNumber = "101",
            Bedrooms = 2,
            MonthlyRent = 1500m,
            UnitTypeId = unitType.Id
        };

        db.Units.Add(unit);
        await db.SaveChangesAsync();
        return unit;
    }

    public static async Task<User> CreateUserAsync(ApplicationDbContext db, string email = "applicant@test.com")
    {
        var user = new User { UserName = email, Email = email };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public static async Task<User> CreateApplicantUserAsync(ApplicationDbContext db, UserManager<User> userManager, string email)
    {
        await TestUserManagerFactory.EnsureApplicantRoleAsync(db);

        var user = new User { UserName = email, Email = email };
        await userManager.CreateAsync(user);
        await userManager.AddToRoleAsync(user, Roles.Applicant);
        return user;
    }

    public static async Task<RentalApplication> CreateApplicationAsync(
        ApplicationDbContext db,
        Unit unit,
        User applicant,
        ApplicationStatus status = ApplicationStatus.Draft,
        bool sectionsComplete = false)
    {
        var application = new RentalApplication
        {
            UnitId = unit.Id,
            ApplicantUserId = applicant.Id,
            Status = status,
            FullName = "Test Applicant",
            PhoneNumber = "555-0100",
            Email = applicant.Email!,
            CurrentAddress = "1 Test Ave",
            ApplicantInfoCompletedAt = sectionsComplete ? DateTime.UtcNow : null,
            ResidenceHistoryCompletedAt = sectionsComplete ? DateTime.UtcNow : null
        };

        db.RentalApplications.Add(application);
        await db.SaveChangesAsync();

        if (sectionsComplete)
        {
            await CreateResidenceAsync(db, application);
        }

        return application;
    }

    public static async Task<Residence> CreateResidenceAsync(ApplicationDbContext db, RentalApplication application)
    {
        var residence = new Residence
        {
            RentalApplicationId = application.Id,
            Address = "10 Prior Ave",
            LandlordName = "Pat Landlord",
            LandlordPhone = "555-0200",
            MoveInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-3)),
            MoveOutDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1))
        };

        db.Residences.Add(residence);
        await db.SaveChangesAsync();
        return residence;
    }

    public static async Task<Lease> CreateLeaseAsync(ApplicationDbContext db, Unit unit, RentalApplication application, DateOnly startDate, DateOnly endDate)
    {
        var lease = new Lease
        {
            UnitId = unit.Id,
            RentalApplicationId = application.Id,
            StartDate = startDate,
            EndDate = endDate
        };

        db.Leases.Add(lease);
        await db.SaveChangesAsync();
        return lease;
    }
}
