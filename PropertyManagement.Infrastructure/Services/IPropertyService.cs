using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Infrastructure.Services;

public interface IPropertyService
{
    Task<List<Property>> GetAllAsync();
    Task<Property?> GetByIdAsync(Guid id);
    Task<Property> CreateAsync(string name, string addressLine1, string? addressLine2, string city, string state, string zipCode);
    Task<bool> UpdateAsync(Guid id, string name, string addressLine1, string? addressLine2, string city, string state, string zipCode);
    Task<bool> DeleteAsync(Guid id);
}
