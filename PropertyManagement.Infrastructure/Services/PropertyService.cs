using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Services;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Infrastructure.Data;

namespace PropertyManagement.Infrastructure.Services;

public class PropertyService(ApplicationDbContext db, IBlobStorageService blobStorage) : IPropertyService
{
    public Task<List<Property>> GetAllAsync()
        => db.Properties.Where(p => !p.IsRemoved).OrderBy(p => p.Name).ToListAsync();

    public Task<Property?> GetByIdAsync(Guid id)
        => db.Properties.FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Property> CreateAsync(string name, string addressLine1, string? addressLine2, string city, string state, string zipCode)
    {
        var property = new Property
        {
            Name = name,
            AddressLine1 = addressLine1,
            AddressLine2 = addressLine2,
            City = city,
            State = state,
            ZipCode = zipCode
        };

        db.Properties.Add(property);
        await db.SaveChangesAsync();

        return property;
    }

    public async Task<bool> UpdateAsync(Guid id, string name, string addressLine1, string? addressLine2, string city, string state, string zipCode)
    {
        var property = await db.Properties.FirstOrDefaultAsync(p => p.Id == id);
        if (property is null)
        {
            return false;
        }

        property.Name = name;
        property.AddressLine1 = addressLine1;
        property.AddressLine2 = addressLine2;
        property.City = city;
        property.State = state;
        property.ZipCode = zipCode;

        await db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var property = await db.Properties.FirstOrDefaultAsync(p => p.Id == id);
        if (property is null)
        {
            return false;
        }

        property.IsRemoved = true;
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        await db.Units
            .Where(u => u.PropertyId == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(u => u.IsRemoved, true)
                .SetProperty(u => u.UpdatedAt, now));

        return true;
    }

    public Task<List<PropertyPhoto>> GetPhotosAsync(Guid propertyId)
        => db.PropertyPhotos
            .Where(p => p.PropertyId == propertyId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();

    public async Task<PropertyPhoto> AddPhotoAsync(Guid propertyId, Stream content, string fileName, string contentType)
    {
        var upload = await blobStorage.UploadAsync($"properties/{propertyId}", fileName, content, contentType);

        var photo = new PropertyPhoto
        {
            PropertyId = propertyId,
            Url = upload.Url,
            BlobName = upload.BlobName
        };

        db.PropertyPhotos.Add(photo);
        await db.SaveChangesAsync();

        return photo;
    }

    public async Task<bool> RemovePhotoAsync(Guid photoId)
    {
        var photo = await db.PropertyPhotos.FirstOrDefaultAsync(p => p.Id == photoId);
        if (photo is null)
        {
            return false;
        }

        await blobStorage.DeleteAsync(photo.BlobName);

        db.PropertyPhotos.Remove(photo);
        await db.SaveChangesAsync();

        return true;
    }
}
