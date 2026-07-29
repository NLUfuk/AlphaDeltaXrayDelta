using CrmKanban.Application.Abstractions;
using CrmKanban.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace CrmKanban.Infrastructure.Identity;

/// <summary>Adapts ASP.NET Identity's PasswordHasher to the Application seam (spec §9).</summary>
public sealed class PasswordHasherAdapter(PasswordHasher<User> hasher) : IPasswordHasher
{
    public string Hash(User user, string password) => hasher.HashPassword(user, password);

    public bool Verify(User user, string hash, string password) =>
        hasher.VerifyHashedPassword(user, hash, password) is
            PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
}
