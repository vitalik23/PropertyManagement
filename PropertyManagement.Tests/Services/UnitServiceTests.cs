using Microsoft.EntityFrameworkCore;
using PropertyManagement.Infrastructure.Services;

namespace PropertyManagement.Tests.Services;

public class UnitServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly UnitService _service;

    public UnitServiceTests()
    {
        _service = new UnitService(_factory.Context);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task CreateAsync_WithInactiveUnitType_Fails()
    {
        var db = _factory.Context;
        var property = await TestDataBuilder.CreatePropertyAsync(db);
        var inactiveType = await TestDataBuilder.CreateUnitTypeAsync(db, isActive: false);

        var result = await _service.CreateAsync(property.Id, "101", 2, 1500m, inactiveType.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(0, await db.Units.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_WithActiveUnitType_Succeeds()
    {
        var db = _factory.Context;
        var property = await TestDataBuilder.CreatePropertyAsync(db);
        var activeType = await TestDataBuilder.CreateUnitTypeAsync(db, isActive: true);

        var result = await _service.CreateAsync(property.Id, "101", 2, 1500m, activeType.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(1, await db.Units.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_ChangingToInactiveType_Fails()
    {
        var db = _factory.Context;
        var activeType = await TestDataBuilder.CreateUnitTypeAsync(db, isActive: true);
        var inactiveType = await TestDataBuilder.CreateUnitTypeAsync(db, isActive: false);
        var unit = await TestDataBuilder.CreateUnitAsync(db, unitType: activeType);

        var result = await _service.UpdateAsync(unit.Id, unit.UnitNumber, unit.Bedrooms, unit.MonthlyRent, inactiveType.Id);

        Assert.False(result.Succeeded);
        var updated = await db.Units.FirstAsync(u => u.Id == unit.Id);
        Assert.Equal(activeType.Id, updated.UnitTypeId);
    }

    [Fact]
    public async Task UpdateAsync_KeepingAlreadyAssignedInactiveType_Succeeds()
    {
        var db = _factory.Context;
        var inactiveType = await TestDataBuilder.CreateUnitTypeAsync(db, isActive: false);
        var unit = await TestDataBuilder.CreateUnitAsync(db, unitType: inactiveType);

        var result = await _service.UpdateAsync(unit.Id, "202", 3, 1800m, inactiveType.Id);

        Assert.True(result.Succeeded);
        var updated = await db.Units.FirstAsync(u => u.Id == unit.Id);
        Assert.Equal("202", updated.UnitNumber);
        Assert.Equal(inactiveType.Id, updated.UnitTypeId);
    }
}
