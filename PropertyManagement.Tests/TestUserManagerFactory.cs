using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PropertyManagement.Domain.Constants;
using PropertyManagement.Domain.Entities;
using PropertyManagement.Infrastructure.Data;

namespace PropertyManagement.Tests;

public static class TestUserManagerFactory
{
    public static UserManager<User> Create(ApplicationDbContext db)
    {
        var store = new UserStore<User, IdentityRole<Guid>, ApplicationDbContext, Guid>(db);

        return new UserManager<User>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            NullLogger<UserManager<User>>.Instance);
    }

    public static async Task EnsureApplicantRoleAsync(ApplicationDbContext db)
    {
        if (!await db.Roles.AnyAsync(r => r.Name == Roles.Applicant))
        {
            db.Roles.Add(new IdentityRole<Guid>(Roles.Applicant) { NormalizedName = Roles.Applicant.ToUpperInvariant() });
            await db.SaveChangesAsync();
        }
    }
}
