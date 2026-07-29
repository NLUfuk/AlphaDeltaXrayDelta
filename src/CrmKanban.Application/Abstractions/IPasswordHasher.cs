using CrmKanban.Domain.Entities;

namespace CrmKanban.Application.Abstractions;

/// <summary>Password hashing seam over ASP.NET Identity's PasswordHasher (spec §9).</summary>
public interface IPasswordHasher
{
    string Hash(User user, string password);

    bool Verify(User user, string hash, string password);
}
