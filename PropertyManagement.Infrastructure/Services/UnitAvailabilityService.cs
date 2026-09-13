using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Infrastructure.Data;

namespace PropertyManagement.Infrastructure.Services;

public class UnitAvailabilityService(ApplicationDbContext db) : IUnitAvailabilityService
{
    public async Task<bool> IsAvailableAsync(Guid unitId, DateOnly today)
        => !await db.Leases.AnyAsync(l => l.UnitId == unitId && l.StartDate <= today && l.EndDate >= today);

    public Task<List<Unit>> GetAvailableUnitsAsync(DateOnly today, Guid? propertyId = null, Guid? unitTypeId = null, int? bedrooms = null, decimal? minRent = null, decimal? maxRent = null)
    {
        var query = db.Units
            .Include(u => u.Property)
            .Include(u => u.UnitType)
            .Include(u => u.Photos)
            .Where(u => !u.IsRemoved && !db.Leases.Any(l => l.UnitId == u.Id && l.StartDate <= today && l.EndDate >= today));

        if (propertyId is not null)
        {
            query = query.Where(u => u.PropertyId == propertyId);
        }

        if (unitTypeId is not null)
        {
            query = query.Where(u => u.UnitTypeId == unitTypeId);
        }

        if (bedrooms is not null)
        {
            // 1-4 mean exactly that many bedrooms; the top bucket (5) stays open-ended ("5+"),
            // matching the dropdown's own label in Home/Index.cshtml.
            query = bedrooms >= 5
                ? query.Where(u => u.Bedrooms >= bedrooms)
                : query.Where(u => u.Bedrooms == bedrooms);
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

    public async Task<(decimal? Min, decimal? Max)> GetRentRangeAsync(DateOnly today)
    {
        var rents = await db.Units
            .Where(u => !u.IsRemoved && !db.Leases.Any(l => l.UnitId == u.Id && l.StartDate <= today && l.EndDate >= today))
            .Select(u => u.MonthlyRent)
            .ToListAsync();

        return rents.Count == 0 ? (null, null) : (rents.Min(), rents.Max());
    }
}
