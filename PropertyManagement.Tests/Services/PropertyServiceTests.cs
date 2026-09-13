using Microsoft.EntityFrameworkCore;
using PropertyManagement.Infrastructure.Services;

namespace PropertyManagement.Tests.Services;

public class PropertyServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly PropertyService _service;

    public PropertyServiceTests()
    {
        _service = new PropertyService(_factory.Context);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task DeleteAsync_MarksPropertyAndItsUnitsRemoved_InsteadOfDeletingRows()
    {
        var db = _factory.Context;
        var property = await TestDataBuilder.CreatePropertyAsync(db);
        var unitType = await TestDataBuilder.CreateUnitTypeAsync(db);
        var unit = await TestDataBuilder.CreateUnitAsync(db, property, unitType);

        var result = await _service.DeleteAsync(property.Id);

        Assert.True(result);

        var stillThereProperty = await db.Properties.FirstAsync(p => p.Id == property.Id);
        Assert.True(stillThereProperty.IsRemoved);

        // AsNoTracking: ExecuteUpdateAsync writes straight to the DB, bypassing the change
        // tracker, so the `unit` instance already tracked from CreateUnitAsync above would
        // otherwise be returned unchanged from the identity map instead of reflecting the update.
        var stillThereUnit = await db.Units.AsNoTracking().FirstAsync(u => u.Id == unit.Id);
        Assert.True(stillThereUnit.IsRemoved);
    }

    [Fact]
    public async Task DeleteAsync_WithExistingRentalApplication_DoesNotThrow()
    {
        // Regression test for the reported bug: Unit -> Property was Cascade while
        // RentalApplication -> Unit was Restrict, so deleting a Property whose unit had a
        // rental application threw a DbUpdateException (FK conflict). Soft-delete means no
        // physical DELETE is ever issued, so this must succeed cleanly regardless of history.
        var db = _factory.Context;
        var property = await TestDataBuilder.CreatePropertyAsync(db);
        var unitType = await TestDataBuilder.CreateUnitTypeAsync(db);
        var unit = await TestDataBuilder.CreateUnitAsync(db, property, unitType);
        var applicant = await TestDataBuilder.CreateUserAsync(db);
        await TestDataBuilder.CreateApplicationAsync(db, unit, applicant);

        var result = await _service.DeleteAsync(property.Id);

        Assert.True(result);
        var stillThere = await db.Properties.FirstAsync(p => p.Id == property.Id);
        Assert.True(stillThere.IsRemoved);
    }

    [Fact]
    public async Task GetAllAsync_ExcludesRemovedProperties()
    {
        var db = _factory.Context;
        var kept = await TestDataBuilder.CreatePropertyAsync(db);
        var removed = await TestDataBuilder.CreatePropertyAsync(db);
        await _service.DeleteAsync(removed.Id);

        var results = await _service.GetAllAsync();

        Assert.Single(results);
        Assert.Equal(kept.Id, results[0].Id);
    }
}
