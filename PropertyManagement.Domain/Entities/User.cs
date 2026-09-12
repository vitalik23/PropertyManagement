using Microsoft.AspNetCore.Identity;
using PropertyManagement.Domain.Entities.Base;

namespace PropertyManagement.Domain.Entities;

public class User : IdentityUser<Guid>, IBaseEntity
{
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
