namespace CrmKanban.Infrastructure.Persistence.Seed;

/// <summary>
/// Demo-environment settings, bound from the "Seed" section. Infrastructure, not a business parameter:
/// it belongs in env/appsettings and must NEVER be editable from the UI (spec §13).
/// </summary>
public sealed class DemoOptions
{
    /// <summary>Whether the demo tenants are seeded at all (also implied by the Development environment).</summary>
    public bool Demo { get; init; }

    /// <summary>
    /// The password every demo account gets. Deliberately has NO default: it used to be a constant in this
    /// file, and since the repository is public that meant anyone reading it could sign in to the live demo
    /// as a company admin (measured 2026-08-13). With no value configured the demo seed does not run at all
    /// — fail closed, because a demo with a guessable shared password is worse than no demo.
    /// </summary>
    public string? DemoPassword { get; init; }

    /// <summary>
    /// How often the demo tenants are wiped and re-seeded, in hours. 0 (default) = never. This is the
    /// second half of the answer to the shared-password problem: whoever gets in cannot do lasting harm,
    /// because the tenant returns to its known state on the next reset.
    /// </summary>
    public int ResetHours { get; init; }

    /// <summary>Minimum length for <see cref="DemoPassword"/>; anything shorter is treated as unset.</summary>
    public const int MinPasswordLength = 12;

    public bool HasUsablePassword => !string.IsNullOrWhiteSpace(DemoPassword) && DemoPassword.Length >= MinPasswordLength;
}

/// <summary>The demo tenants, named once. The seeder creates exactly these and the reset job removes
/// exactly these — one list, so a company added to the demo can never be missed by the cleanup.</summary>
public static class DemoTenants
{
    public const string Tekstil = "tekstil";
    public const string TekstilIhracat = "tekstil-ihracat";
    public const string Mermer = "mermer";
    public const string Yazilim = "yazilim";
    public const string Lojistik = "lojistik";

    public static readonly string[] Slugs = [Tekstil, TekstilIhracat, Mermer, Yazilim, Lojistik];
}
