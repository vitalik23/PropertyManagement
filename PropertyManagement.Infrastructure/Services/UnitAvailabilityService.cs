using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Infrastructure.Data;

namespace PropertyManagement.Infrastructure.Services;

public class UnitAvailabilityService(ApplicationDbContext db) : IUnitAvailabilityService
{
    public async Task<bool> IsAvailableAsync(Guid unitId, DateOnly today)
        => !await db.Leases.AnyAsync(l => l.UnitId == unitId && l.StartDate <= today && l.EndDate >= today);

    public Task<List<Unit>> GetAvailableUnitsAsync(DateOnly today)
        => db.Units
            .Include(u => u.Property)
            .Include(u => u.UnitType)
            .Where(u => !db.Leases.Any(l => l.UnitId == u.Id && l.StartDate <= today && l.EndDate >= today))
            .OrderBy(u => u.Property.Name)
            .ThenBy(u => u.UnitNumber)
            .ToListAsync();
}
