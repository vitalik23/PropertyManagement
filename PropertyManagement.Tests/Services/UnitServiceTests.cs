using Microsoft.EntityFrameworkCore;
using PropertyManagement.Infrastructure.Services;

namespace PropertyManagement.Tests.Services;

public class UnitServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly FakeBlobStorageService _blobStorage = new();
    private readonly UnitService _service;

    public UnitServiceTests()
    {
        _service = new UnitService(_factory.Context, _blobStorage);
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

    [Fact]
    public async Task DeleteAsync_MarksUnitRemoved_InsteadOfDeletingTheRow()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);

        var result = await _service.DeleteAsync(unit.Id);

        Assert.True(result);
        var stillThere = await db.Units.FirstAsync(u => u.Id == unit.Id);
        Assert.True(stillThere.IsRemoved);
    }

    [Fact]
    public async Task GetByPropertyIdAsync_ExcludesRemovedUnits()
    {
        var db = _factory.Context;
        var property = await TestDataBuilder.CreatePropertyAsync(db);
        var unitType = await TestDataBuilder.CreateUnitTypeAsync(db);
        var keptUnit = await TestDataBuilder.CreateUnitAsync(db, property, unitType);
        var removedUnit = await TestDataBuilder.CreateUnitAsync(db, property, unitType);
        await _service.DeleteAsync(removedUnit.Id);

        var results = await _service.GetByPropertyIdAsync(property.Id);

        Assert.Single(results);
        Assert.Equal(keptUnit.Id, results[0].Id);
    }

    [Fact]
    public async Task CreateAsync_UnderRemovedProperty_Fails()
    {
        var db = _factory.Context;
        var property = await TestDataBuilder.CreatePropertyAsync(db);
        var unitType = await TestDataBuilder.CreateUnitTypeAsync(db);
        property.IsRemoved = true;
        await db.SaveChangesAsync();

        var result = await _service.CreateAsync(property.Id, "101", 2, 1500m, unitType.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(0, await db.Units.CountAsync());
    }

    [Fact]
    public async Task AddPhotoAsync_UploadsToBlobStorageAndSavesPhotoRow()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        using var content = new MemoryStream([1, 2, 3]);

        var photo = await _service.AddPhotoAsync(unit.Id, content, "photo.jpg", "image/jpeg");

        Assert.Equal(unit.Id, photo.UnitId);
        Assert.False(string.IsNullOrEmpty(photo.Url));
        Assert.False(string.IsNullOrEmpty(photo.BlobName));
        Assert.Single(_blobStorage.UploadedFolders, $"units/{unit.Id}");
        Assert.Equal(1, await db.UnitPhotos.CountAsync(p => p.UnitId == unit.Id));
    }

    [Fact]
    public async Task GetPhotosAsync_ReturnsOnlyPhotosForThatUnit_InUploadOrder()
    {
        var db = _factory.Context;
        var unitA = await TestDataBuilder.CreateUnitAsync(db);
        var unitB = await TestDataBuilder.CreateUnitAsync(db);
        using var content = new MemoryStream([1]);
        var first = await _service.AddPhotoAsync(unitA.Id, content, "a.jpg", "image/jpeg");
        content.Position = 0;
        var second = await _service.AddPhotoAsync(unitA.Id, content, "b.jpg", "image/jpeg");
        content.Position = 0;
        await _service.AddPhotoAsync(unitB.Id, content, "c.jpg", "image/jpeg");

        var photos = await _service.GetPhotosAsync(unitA.Id);

        Assert.Equal(2, photos.Count);
        Assert.Equal(first.Id, photos[0].Id);
        Assert.Equal(second.Id, photos[1].Id);
    }

    [Fact]
    public async Task RemovePhotoAsync_DeletesBlobAndRemovesRow()
    {
        var db = _factory.Context;
        var unit = await TestDataBuilder.CreateUnitAsync(db);
        using var content = new MemoryStream([1]);
        var photo = await _service.AddPhotoAsync(unit.Id, content, "photo.jpg", "image/jpeg");

        var result = await _service.RemovePhotoAsync(photo.Id);

        Assert.True(result);
        Assert.Single(_blobStorage.DeletedBlobNames, photo.BlobName);
        Assert.Equal(0, await db.UnitPhotos.CountAsync());
    }

    [Fact]
    public async Task RemovePhotoAsync_WithUnknownId_ReturnsFalse()
    {
        var result = await _service.RemovePhotoAsync(Guid.NewGuid());

        Assert.False(result);
    }
}
