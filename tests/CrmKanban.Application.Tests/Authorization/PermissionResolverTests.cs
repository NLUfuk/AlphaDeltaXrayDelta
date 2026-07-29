using CrmKanban.Application.Authorization;
using CrmKanban.Domain.Enums;
using FluentAssertions;

namespace CrmKanban.Application.Tests.Authorization;

public class PermissionResolverTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.NewGuid();

    [Fact]
    public void Baseline_is_returned_when_no_overrides()
    {
        var result = PermissionResolver.Resolve(["ticket.view", "ticket.edit"], [], CompanyA);
        result.Should().BeEquivalentTo(["ticket.view", "ticket.edit"]);
    }

    [Fact]
    public void Grant_adds_a_permission_not_in_the_baseline()
    {
        var result = PermissionResolver.Resolve(
            ["ticket.view"],
            [new PermissionOverride("ticket.delete", UserPermissionType.Grant, CompanyA)],
            CompanyA);

        result.Should().Contain("ticket.delete");
    }

    [Fact]
    public void Deny_removes_a_permission_from_the_baseline()
    {
        var result = PermissionResolver.Resolve(
            ["ticket.view", "ticket.delete"],
            [new PermissionOverride("ticket.delete", UserPermissionType.Deny, CompanyA)],
            CompanyA);

        result.Should().NotContain("ticket.delete");
        result.Should().Contain("ticket.view");
    }

    [Fact]
    public void Deny_wins_over_a_grant_on_the_same_key()
    {
        var result = PermissionResolver.Resolve(
            [],
            [
                new PermissionOverride("ticket.delete", UserPermissionType.Grant, CompanyA),
                new PermissionOverride("ticket.delete", UserPermissionType.Deny, CompanyA),
            ],
            CompanyA);

        result.Should().NotContain("ticket.delete", "deny always wins over grant");
    }

    [Fact]
    public void Overrides_scoped_to_another_company_do_not_apply()
    {
        var result = PermissionResolver.Resolve(
            ["ticket.view"],
            [new PermissionOverride("ticket.delete", UserPermissionType.Grant, CompanyB)],
            CompanyA);

        result.Should().NotContain("ticket.delete", "the grant is scoped to company B");
    }

    [Fact]
    public void Global_override_applies_in_any_company()
    {
        var result = PermissionResolver.Resolve(
            [],
            [new PermissionOverride("report.global", UserPermissionType.Grant, CompanyId: null)],
            CompanyA);

        result.Should().Contain("report.global");
    }
}
