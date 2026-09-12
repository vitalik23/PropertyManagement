using Microsoft.AspNetCore.Identity;
using PropertyManagement.Domain.Constants;

namespace PropertyManagement.Infrastructure.Data.Seed;

public class RoleSeeder(RoleManager<IdentityRole<Guid>> roleManager) : IRoleSeeder
{
    public async Task SeedAsync()
    {
        foreach (var roleName in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            }
        }
    }
}
