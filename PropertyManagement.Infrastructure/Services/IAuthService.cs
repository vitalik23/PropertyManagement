using Microsoft.AspNetCore.Identity;

namespace PropertyManagement.Infrastructure.Services;

public interface IAuthService
{
    Task<IdentityResult> RegisterAsync(string email, string password, string role);
    Task<SignInResult> LoginAsync(string email, string password, bool rememberMe);
    Task LogoutAsync();
}
