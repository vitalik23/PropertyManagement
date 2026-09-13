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

    public async Task<IdentityResult> UpdateProfileAsync(Guid userId, string email, string? phoneNumber)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return IdentityResult.Failed(new IdentityError { Description = "User was not found." });
        }

        // UserName mirrors Email throughout this app (see RegisterAsync). RequireUniqueEmail isn't
        // enabled, so SetEmailAsync alone wouldn't reject a duplicate — and since SetEmailAsync and
        // SetUserNameAsync each persist independently, letting SetUserNameAsync's uniqueness check
        // be the only guard risks committing the new (duplicate) email while the username update
        // then fails, leaving Email/UserName out of sync. Check uniqueness ourselves first instead.
        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await userManager.FindByEmailAsync(email);
            if (existing is not null && existing.Id != user.Id)
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "DuplicateEmail",
                    Description = $"Email '{email}' is already taken."
                });
            }

            var setEmailResult = await userManager.SetEmailAsync(user, email);
            if (!setEmailResult.Succeeded)
            {
                return setEmailResult;
            }

            var setUserNameResult = await userManager.SetUserNameAsync(user, email);
            if (!setUserNameResult.Succeeded)
            {
                return setUserNameResult;
            }
        }

        if (user.PhoneNumber != phoneNumber)
        {
            var setPhoneResult = await userManager.SetPhoneNumberAsync(user, phoneNumber);
            if (!setPhoneResult.Succeeded)
            {
                return setPhoneResult;
            }
        }

        // Keep the active session's cookie claims (Name, etc.) in sync with the new values.
        await signInManager.RefreshSignInAsync(user);

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return IdentityResult.Failed(new IdentityError { Description = "User was not found." });
        }

        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (result.Succeeded)
        {
            // Changing the password bumps the security stamp — refresh now or the current
            // session gets signed out on its next cookie validation.
            await signInManager.RefreshSignInAsync(user);
        }

        return result;
    }
}
