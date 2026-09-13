using Microsoft.EntityFrameworkCore;
using PropertyManagement.Application.Services;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Infrastructure.Data;

namespace PropertyManagement.Infrastructure.Services;

public class UnitTypeService(ApplicationDbContext db) : IUnitTypeService
{
    public Task<List<UnitType>> GetAllAsync()
        => db.UnitTypes.OrderBy(t => t.Name).ToListAsync();

    public Task<UnitType?> GetByIdAsync(Guid id)
        => db.UnitTypes.FirstOrDefaultAsync(t => t.Id == id);

    public async Task<UnitType> CreateAsync(string name, bool isActive)
    {
        var unitType = new UnitType { Name = name, IsActive = isActive };
        db.UnitTypes.Add(unitType);
        await db.SaveChangesAsync();
        return unitType;
    }

    public async Task<bool> UpdateAsync(Guid id, string name, bool isActive)
    {
        var unitType = await db.UnitTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (unitType is null)
        {
            return false;
        }

        unitType.Name = name;
        unitType.IsActive = isActive;
        await db.SaveChangesAsync();

        return true;
    }
}
