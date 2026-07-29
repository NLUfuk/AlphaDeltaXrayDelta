using CrmKanban.Domain.Common;

namespace CrmKanban.Domain.Entities;

/// <summary>
/// A person who can log in (spec §11). Email is globally unique. Password is stored as a
/// hash only (ASP.NET Identity PasswordHasher, spec §9). Users with no PasswordHash yet are
/// "invited" — they set their password via an invite link before they can log in.
/// </summary>
public sealed class User : Entity
{
    private User() { } // EF

    public User(string email, string firstName, string lastName)
    {
        Email = email.Trim().ToLowerInvariant();
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        IsActive = true;
    }

    public string Email { get; private set; } = null!;
    public string FirstName { get; private set; } = null!;
    public string LastName { get; private set; } = null!;
    public string? PasswordHash { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>Global super admin (spec §8) — bypasses the tenant filter, has every permission.
    /// A fixed role modeled as a flag, not a Membership (SuperAdmin is not company-scoped).</summary>
    public bool IsSuperAdmin { get; private set; }

    public void PromoteToSuperAdmin() => IsSuperAdmin = true;

    /// <summary>Forces a password change on next login (spec §9: seeded super admin must rotate).</summary>
    public bool MustChangePassword { get; private set; }

    /// <summary>True until the user has set a password (invited-but-not-activated).</summary>
    public bool IsInvitedPending => PasswordHash is null;

    public void SetPasswordHash(string hash, bool mustChange = false)
    {
        PasswordHash = hash;
        MustChangePassword = mustChange;
    }

    public void ClearMustChangePassword() => MustChangePassword = false;

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;

    public void Rename(string firstName, string lastName)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
    }
}
