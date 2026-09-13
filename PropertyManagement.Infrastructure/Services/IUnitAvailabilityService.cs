using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Infrastructure.Services;

public interface IUnitAvailabilityService
{
    Task<bool> IsAvailableAsync(Guid unitId, DateOnly today);
    Task<List<Unit>> GetAvailableUnitsAsync(DateOnly today, Guid? propertyId = null, Guid? unitTypeId = null, int? minBedrooms = null, decimal? minRent = null, decimal? maxRent = null);
}
