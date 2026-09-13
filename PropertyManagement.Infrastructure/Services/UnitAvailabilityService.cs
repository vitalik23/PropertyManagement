using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Infrastructure.Data;

namespace PropertyManagement.Infrastructure.Services;

public class UnitAvailabilityService(ApplicationDbContext db) : IUnitAvailabilityService
{
    public async Task<bool> IsAvailableAsync(Guid unitId, DateOnly today)
        => !await db.Leases.AnyAsync(l => l.UnitId == unitId && l.StartDate <= today && l.EndDate >= today);

    public Task<List<Unit>> GetAvailableUnitsAsync(DateOnly today, Guid? propertyId = null, Guid? unitTypeId = null, int? minBedrooms = null, decimal? minRent = null, decimal? maxRent = null)
    {
        var query = db.Units
            .Include(u => u.Property)
            .Include(u => u.UnitType)
            .Where(u => !u.IsRemoved && !db.Leases.Any(l => l.UnitId == u.Id && l.StartDate <= today && l.EndDate >= today));

        if (propertyId is not null)
        {
            query = query.Where(u => u.PropertyId == propertyId);
        }

        if (unitTypeId is not null)
        {
            query = query.Where(u => u.UnitTypeId == unitTypeId);
        }

        if (minBedrooms is not null)
        {
            query = query.Where(u => u.Bedrooms >= minBedrooms);
        }

        if (minRent is not null)
        {
            query = query.Where(u => u.MonthlyRent >= minRent);
        }

        if (maxRent is not null)
        {
            query = query.Where(u => u.MonthlyRent <= maxRent);
        }

        return query
            .OrderBy(u => u.Property.Name)
            .ThenBy(u => u.UnitNumber)
            .ToListAsync();
    }
}
