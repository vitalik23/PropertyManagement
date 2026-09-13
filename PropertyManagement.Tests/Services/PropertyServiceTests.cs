using Microsoft.EntityFrameworkCore;
using PropertyManagement.Infrastructure.Services;

namespace PropertyManagement.Tests.Services;

public class PropertyServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly FakeBlobStorageService _blobStorage = new();
    private readonly PropertyService _service;

    public PropertyServiceTests()
    {
        _service = new PropertyService(_factory.Context, _blobStorage);
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

    [Fact]
    public async Task AddPhotoAsync_UploadsToBlobStorageAndSavesPhotoRow()
    {
        var db = _factory.Context;
        var property = await TestDataBuilder.CreatePropertyAsync(db);
        using var content = new MemoryStream([1, 2, 3]);

        var photo = await _service.AddPhotoAsync(property.Id, content, "photo.jpg", "image/jpeg");

        Assert.Equal(property.Id, photo.PropertyId);
        Assert.False(string.IsNullOrEmpty(photo.Url));
        Assert.False(string.IsNullOrEmpty(photo.BlobName));
        Assert.Single(_blobStorage.UploadedFolders, $"properties/{property.Id}");
        Assert.Equal(1, await db.PropertyPhotos.CountAsync(p => p.PropertyId == property.Id));
    }

    [Fact]
    public async Task RemovePhotoAsync_DeletesBlobAndRemovesRow()
    {
        var db = _factory.Context;
        var property = await TestDataBuilder.CreatePropertyAsync(db);
        using var content = new MemoryStream([1]);
        var photo = await _service.AddPhotoAsync(property.Id, content, "photo.jpg", "image/jpeg");

        var result = await _service.RemovePhotoAsync(photo.Id);

        Assert.True(result);
        Assert.Single(_blobStorage.DeletedBlobNames, photo.BlobName);
        Assert.Equal(0, await db.PropertyPhotos.CountAsync());
    }
}
