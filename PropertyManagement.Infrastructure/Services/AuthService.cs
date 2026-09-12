using Microsoft.AspNetCore.Identity;
using PropertyManagement.Domain.Entities;

namespace PropertyManagement.Infrastructure.Services;

public class AuthService(UserManager<User> userManager, SignInManager<User> signInManager) : IAuthService
{
    public async Task<IdentityResult> RegisterAsync(string email, string password, string role)
    {
        var user = new User { UserName = email, Email = email };
        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            return createResult;
        }

        var roleResult = await userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            return roleResult;
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return IdentityResult.Success;
    }

    public Task<SignInResult> LoginAsync(string email, string password, bool rememberMe)
        => signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: true);

    public Task LogoutAsync() => signInManager.SignOutAsync();
}
