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
}
