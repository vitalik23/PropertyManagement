using PropertyManagement.Infrastructure.Services;

namespace PropertyManagement.Tests.Services;

public class UnitAvailabilityServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly UnitAvailabilityService _service;

    public UnitAvailabilityServiceTests()
    {
        _service = new UnitAvailabilityService(_factory.Context);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task IsAvailableAsync_ReturnsFalse_WhenLeaseCoversToday()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var user = await TestDataBuilder.CreateUserAsync(db);
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, user);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await TestDataBuilder.CreateLeaseAsync(db, unit, application, today.AddDays(-10), today.AddDays(10));

        var available = await _service.IsAvailableAsync(unit.Id, today);

        Assert.False(available);
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsTrue_WhenLeaseIsEntirelyInThePast()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var user = await TestDataBuilder.CreateUserAsync(db);
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, user);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await TestDataBuilder.CreateLeaseAsync(db, unit, application, today.AddYears(-1), today.AddDays(-1));

        var available = await _service.IsAvailableAsync(unit.Id, today);

        Assert.True(available);
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsTrue_WhenLeaseIsEntirelyInTheFuture()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var user = await TestDataBuilder.CreateUserAsync(db);
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, user);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await TestDataBuilder.CreateLeaseAsync(db, unit, application, today.AddDays(1), today.AddYears(1));

        var available = await _service.IsAvailableAsync(unit.Id, today);

        Assert.True(available);
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsFalse_WhenLeaseStartsToday()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var user = await TestDataBuilder.CreateUserAsync(db);
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, user);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await TestDataBuilder.CreateLeaseAsync(db, unit, application, today, today.AddMonths(12));

        var available = await _service.IsAvailableAsync(unit.Id, today);

        Assert.False(available);
    }

    [Fact]
    public async Task IsAvailableAsync_ReturnsFalse_WhenLeaseEndsToday()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        var user = await TestDataBuilder.CreateUserAsync(db);
        var application = await TestDataBuilder.CreateApplicationAsync(db, unit, user);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await TestDataBuilder.CreateLeaseAsync(db, unit, application, today.AddMonths(-12), today);

        var available = await _service.IsAvailableAsync(unit.Id, today);

        Assert.False(available);
    }

    [Fact]
    public async Task GetAvailableUnitsAsync_ExcludesLeasedUnit_IncludesFreeUnit()
    {
        var db = _factory.Context;
        var property = await TestDataBuilder.CreatePropertyAsync(db);
        var unitType = await TestDataBuilder.CreateUnitTypeAsync(db);
        var leasedUnit = await TestDataBuilder.CreateUnitAsync(db, property, unitType);
        var freeUnit = await TestDataBuilder.CreateUnitAsync(db, property, unitType);
        var user = await TestDataBuilder.CreateUserAsync(db);
        var application = await TestDataBuilder.CreateApplicationAsync(db, leasedUnit, user);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await TestDataBuilder.CreateLeaseAsync(db, leasedUnit, application, today.AddDays(-1), today.AddDays(1));

        var available = await _service.GetAvailableUnitsAsync(today);

        Assert.DoesNotContain(available, u => u.Id == leasedUnit.Id);
        Assert.Contains(available, u => u.Id == freeUnit.Id);
    }

    [Fact]
    public async Task GetAvailableUnitsAsync_FiltersByProperty()
    {
        var db = _factory.Context;
        var unitType = await TestDataBuilder.CreateUnitTypeAsync(db);
        var propertyA = await TestDataBuilder.CreatePropertyAsync(db);
        var propertyB = await TestDataBuilder.CreatePropertyAsync(db);
        var unitA = await TestDataBuilder.CreateUnitAsync(db, propertyA, unitType);
        await TestDataBuilder.CreateUnitAsync(db, propertyB, unitType);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var results = await _service.GetAvailableUnitsAsync(today, propertyId: propertyA.Id);

        Assert.Single(results);
        Assert.Equal(unitA.Id, results[0].Id);
    }

    [Fact]
    public async Task GetAvailableUnitsAsync_FiltersByUnitType()
    {
        var db = _factory.Context;
        var property = await TestDataBuilder.CreatePropertyAsync(db);
        var typeA = await TestDataBuilder.CreateUnitTypeAsync(db);
        var typeB = await TestDataBuilder.CreateUnitTypeAsync(db);
        var unitA = await TestDataBuilder.CreateUnitAsync(db, property, typeA);
        await TestDataBuilder.CreateUnitAsync(db, property, typeB);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var results = await _service.GetAvailableUnitsAsync(today, unitTypeId: typeA.Id);

        Assert.Single(results);
        Assert.Equal(unitA.Id, results[0].Id);
    }

    [Fact]
    public async Task GetAvailableUnitsAsync_FiltersByBedrooms()
    {
        var db = _factory.Context;
        var property = await TestDataBuilder.CreatePropertyAsync(db);
        var unitType = await TestDataBuilder.CreateUnitTypeAsync(db);
        var smallUnit = await TestDataBuilder.CreateUnitAsync(db, property, unitType);
        smallUnit.Bedrooms = 1;
        var bigUnit = await TestDataBuilder.CreateUnitAsync(db, property, unitType);
        bigUnit.Bedrooms = 3;
        await db.SaveChangesAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var results = await _service.GetAvailableUnitsAsync(today, bedrooms: 3);

        Assert.Single(results);
        Assert.Equal(bigUnit.Id, results[0].Id);
    }

    [Fact]
    public async Task GetAvailableUnitsAsync_FiltersByExactBedrooms_ExcludesHigherCounts()
    {
        var db = _factory.Context;
        var property = await TestDataBuilder.CreatePropertyAsync(db);
        var unitType = await TestDataBuilder.CreateUnitTypeAsync(db);
        var twoBed = await TestDataBuilder.CreateUnitAsync(db, property, unitType);
        twoBed.Bedrooms = 2;
        var threeBed = await TestDataBuilder.CreateUnitAsync(db, property, unitType);
        threeBed.Bedrooms = 3;
        var fourBed = await TestDataBuilder.CreateUnitAsync(db, property, unitType);
        fourBed.Bedrooms = 4;
        await db.SaveChangesAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var results = await _service.GetAvailableUnitsAsync(today, bedrooms: 3);

        Assert.Single(results);
        Assert.Equal(threeBed.Id, results[0].Id);
    }

    [Fact]
    public async Task GetAvailableUnitsAsync_BedroomsFiveOrMore_IsOpenEnded()
    {
        var db = _factory.Context;
        var property = await TestDataBuilder.CreatePropertyAsync(db);
        var unitType = await TestDataBuilder.CreateUnitTypeAsync(db);
        var fiveBed = await TestDataBuilder.CreateUnitAsync(db, property, unitType);
        fiveBed.Bedrooms = 5;
        var sixBed = await TestDataBuilder.CreateUnitAsync(db, property, unitType);
        sixBed.Bedrooms = 6;
        await db.SaveChangesAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var results = await _service.GetAvailableUnitsAsync(today, bedrooms: 5);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task GetAvailableUnitsAsync_FiltersByRentRange()
    {
        var db = _factory.Context;
        var property = await TestDataBuilder.CreatePropertyAsync(db);
        var unitType = await TestDataBuilder.CreateUnitTypeAsync(db);
        var cheapUnit = await TestDataBuilder.CreateUnitAsync(db, property, unitType);
        cheapUnit.MonthlyRent = 800m;
        var midUnit = await TestDataBuilder.CreateUnitAsync(db, property, unitType);
        midUnit.MonthlyRent = 1500m;
        var expensiveUnit = await TestDataBuilder.CreateUnitAsync(db, property, unitType);
        expensiveUnit.MonthlyRent = 3000m;
        await db.SaveChangesAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var results = await _service.GetAvailableUnitsAsync(today, minRent: 1000m, maxRent: 2000m);

        Assert.Single(results);
        Assert.Equal(midUnit.Id, results[0].Id);
    }

    [Fact]
    public async Task GetAvailableUnitsAsync_CombinedFilters_Narrow()
    {
        var db = _factory.Context;
        var propertyA = await TestDataBuilder.CreatePropertyAsync(db);
        var propertyB = await TestDataBuilder.CreatePropertyAsync(db);
        var unitType = await TestDataBuilder.CreateUnitTypeAsync(db);
        var matching = await TestDataBuilder.CreateUnitAsync(db, propertyA, unitType);
        matching.MonthlyRent = 1200m;
        var wrongProperty = await TestDataBuilder.CreateUnitAsync(db, propertyB, unitType);
        wrongProperty.MonthlyRent = 1200m;
        var wrongRent = await TestDataBuilder.CreateUnitAsync(db, propertyA, unitType);
        wrongRent.MonthlyRent = 5000m;
        await db.SaveChangesAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var results = await _service.GetAvailableUnitsAsync(today, propertyId: propertyA.Id, maxRent: 2000m);

        Assert.Single(results);
        Assert.Equal(matching.Id, results[0].Id);
    }
}
