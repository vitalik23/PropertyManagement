using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Application.Services;

public record UnitSaveResult(bool Succeeded, string? Error = null)
{
    public static UnitSaveResult Success() => new(true);
    public static UnitSaveResult Failure(string error) => new(false, error);
}

public interface IUnitService
{
    Task<List<Unit>> GetByPropertyIdAsync(Guid propertyId);
    Task<Unit?> GetByIdAsync(Guid id);
    Task<List<UnitType>> GetSelectableUnitTypesAsync(Guid? currentUnitTypeId);
    Task<UnitSaveResult> CreateAsync(Guid propertyId, string unitNumber, int bedrooms, decimal monthlyRent, Guid unitTypeId);
    Task<UnitSaveResult> UpdateAsync(Guid id, string unitNumber, int bedrooms, decimal monthlyRent, Guid unitTypeId);
    Task<bool> DeleteAsync(Guid id);
    Task<List<UnitPhoto>> GetPhotosAsync(Guid unitId);
    Task<UnitPhoto> AddPhotoAsync(Guid unitId, Stream content, string fileName, string contentType);
    Task<bool> RemovePhotoAsync(Guid photoId);
}
