using System.Reflection;
using CrmKanban.Application.Authorization;
using CrmKanban.Domain.Authorization;
using FluentAssertions;

namespace CrmKanban.Application.Tests.Authorization;

/// <summary>
/// Tech debt #40: a permission key that nothing enforces is worse than no key — the admin UI offers a
/// switch that changes nothing (misleading), or, as in Faz 30's `ticket.view`, a guard everyone assumed
/// existed. Three separate rounds found silent drift here, so the match is checked mechanically instead
/// of by eye: every key in <see cref="PermissionKeys.All"/> must either be referenced from Application/Api
/// code, or be declared global-only (enforced by a direct "is super admin?" check, so a per-company grant
/// could never satisfy it — see <see cref="PermissionLabels.IsGlobalOnly"/>).
/// </summary>
public class PermissionEnforcementAuditTests
{
    /// <summary>Where enforcement lives: Application services and the API surface (seed/label files are
    /// deliberately excluded — listing a key is not enforcing it).</summary>
    private static readonly string[] EnforcementProjects = ["src/CrmKanban.Application", "src/CrmKanban.Api"];

    [Fact]
    public void Every_permission_key_is_enforced_somewhere_or_declared_global_only()
    {
        var root = RepoRoot();
        var code = EnforcementProjects
            .SelectMany(dir => Directory.EnumerateFiles(Path.Combine(root, dir), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(File.ReadAllText)
            .ToList();

        // Match on the constant reference (`PermissionKeys.TicketView`), not the raw string, so the label
        // catalog's literals can't stand in for a real enforcement site.
        var unenforced = typeof(PermissionKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Where(f => !PermissionLabels.IsGlobalOnly((string)f.GetValue(null)!))
            .Where(f => !code.Any(src => src.Contains($"PermissionKeys.{f.Name}", StringComparison.Ordinal)))
            .Select(f => (string)f.GetValue(null)!)
            .ToList();

        unenforced.Should().BeEmpty(
            "every permission key must be checked somewhere in Application/Api, or declared global-only in PermissionLabels");
    }

    /// <summary>Every key the UI offers must also be seeded, and vice versa — a key the seed forgets can
    /// never be granted, and a seeded key the catalog forgets is invisible.</summary>
    [Fact]
    public void Every_permission_key_has_a_label_and_a_description()
    {
        foreach (var key in PermissionKeys.All)
        {
            PermissionLabels.ForKey(key).Should().NotBe(key, $"'{key}' needs a Turkish label");
            PermissionLabels.DescriptionFor(key).Should().NotBeNullOrWhiteSpace($"'{key}' needs a description");
        }
    }

    /// <summary>
    /// The remaining Open/Closed hazard in this design, closed mechanically instead of by abstraction.
    /// A new permission is "extension" everywhere it matters — it is a row in the seeded permission
    /// table and a switch in the admin UI, no enforcement code branches on the key set — but declaring
    /// one still means editing <see cref="PermissionKeys"/> in two spots: the const AND the
    /// <see cref="PermissionKeys.All"/> list the seed reads. Omitting the second compiles, passes every
    /// other test, and simply never creates the permission row, so the key can never be granted.
    ///
    /// <para>Building a catalog abstraction to remove the second edit would move Turkish UI text into
    /// the domain to save one line. Making the omission fail the build is the smaller correct fix.</para>
    /// </summary>
    [Fact]
    public void Every_declared_permission_constant_is_in_the_All_list_the_seed_reads()
    {
        var declared = typeof(PermissionKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .ToList();

        declared.Should().BeSubsetOf(PermissionKeys.All,
            "a key missing from PermissionKeys.All is never seeded, so it can never be granted to anyone");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CrmKanban.slnx")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the audit needs the source tree; run tests from the repo");
        return dir!.FullName;
    }
}
