using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Application.Services;

public interface IUnitTypeService
{
    Task<List<UnitType>> GetAllAsync();
    Task<UnitType?> GetByIdAsync(Guid id);
    Task<UnitType> CreateAsync(string name, bool isActive);
    Task<bool> UpdateAsync(Guid id, string name, bool isActive);
}
