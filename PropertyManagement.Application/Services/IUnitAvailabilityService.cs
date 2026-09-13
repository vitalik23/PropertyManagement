using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Application.Services;

public interface IUnitAvailabilityService
{
    Task<bool> IsAvailableAsync(Guid unitId, DateOnly today);
    Task<List<Unit>> GetAvailableUnitsAsync(DateOnly today, Guid? propertyId = null, Guid? unitTypeId = null, int? bedrooms = null, decimal? minRent = null, decimal? maxRent = null);
    Task<(decimal? Min, decimal? Max)> GetRentRangeAsync(DateOnly today);
}
