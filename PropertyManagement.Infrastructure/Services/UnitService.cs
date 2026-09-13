using Microsoft.EntityFrameworkCore;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Infrastructure.Data;

namespace PropertyManagement.Infrastructure.Services;

public class UnitService(ApplicationDbContext db) : IUnitService
{
    public Task<List<Unit>> GetByPropertyIdAsync(Guid propertyId)
        => db.Units
            .Include(u => u.UnitType)
            .Where(u => u.PropertyId == propertyId && !u.IsRemoved)
            .OrderBy(u => u.UnitNumber)
            .ToListAsync();

    public Task<Unit?> GetByIdAsync(Guid id)
        => db.Units.Include(u => u.UnitType).FirstOrDefaultAsync(u => u.Id == id);

    public async Task<List<UnitType>> GetSelectableUnitTypesAsync(Guid? currentUnitTypeId)
    {
        var unitTypes = await db.UnitTypes
            .Where(t => t.IsActive || (currentUnitTypeId != null && t.Id == currentUnitTypeId))
            .OrderBy(t => t.Name)
            .ToListAsync();

        return unitTypes;
    }

    public async Task<UnitSaveResult> CreateAsync(Guid propertyId, string unitNumber, int bedrooms, decimal monthlyRent, Guid unitTypeId)
    {
        var property = await db.Properties.FirstOrDefaultAsync(p => p.Id == propertyId);
        if (property is null)
        {
            return UnitSaveResult.Failure("Property was not found.");
        }

        if (property.IsRemoved)
        {
            return UnitSaveResult.Failure("This property has been removed and cannot receive new units.");
        }

        var unitType = await db.UnitTypes.FirstOrDefaultAsync(t => t.Id == unitTypeId);
        if (unitType is null)
        {
            return UnitSaveResult.Failure("Selected unit type was not found.");
        }

        if (!unitType.IsActive)
        {
            return UnitSaveResult.Failure("Selected unit type is inactive and cannot be used for a new unit.");
        }

        var unit = new Unit
        {
            PropertyId = propertyId,
            UnitNumber = unitNumber,
            Bedrooms = bedrooms,
            MonthlyRent = monthlyRent,
            UnitTypeId = unitTypeId
        };

        db.Units.Add(unit);
        await db.SaveChangesAsync();

        return UnitSaveResult.Success();
    }

    public async Task<UnitSaveResult> UpdateAsync(Guid id, string unitNumber, int bedrooms, decimal monthlyRent, Guid unitTypeId)
    {
        var unit = await db.Units.FirstOrDefaultAsync(u => u.Id == id);
        if (unit is null)
        {
            return UnitSaveResult.Failure("Unit was not found.");
        }

        if (unit.UnitTypeId != unitTypeId)
        {
            var unitType = await db.UnitTypes.FirstOrDefaultAsync(t => t.Id == unitTypeId);
            if (unitType is null)
            {
                return UnitSaveResult.Failure("Selected unit type was not found.");
            }

            if (!unitType.IsActive)
            {
                return UnitSaveResult.Failure("Selected unit type is inactive and cannot be selected for this unit.");
            }
        }

        unit.UnitNumber = unitNumber;
        unit.Bedrooms = bedrooms;
        unit.MonthlyRent = monthlyRent;
        unit.UnitTypeId = unitTypeId;

        await db.SaveChangesAsync();

        return UnitSaveResult.Success();
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var unit = await db.Units.FirstOrDefaultAsync(u => u.Id == id);
        if (unit is null)
        {
            return false;
        }

        unit.IsRemoved = true;
        await db.SaveChangesAsync();

        return true;
    }
}
