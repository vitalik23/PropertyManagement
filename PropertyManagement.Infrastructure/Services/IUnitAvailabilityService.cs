using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Infrastructure.Services;

public interface IUnitAvailabilityService
{
    Task<bool> IsAvailableAsync(Guid unitId, DateOnly today);
    Task<List<Unit>> GetAvailableUnitsAsync(DateOnly today);
}
