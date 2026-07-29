using CrmKanban.Application.Authorization;
using CrmKanban.Application.Common;
using FluentAssertions;

namespace CrmKanban.Application.Tests.Authorization;

public class PermissionAssignmentGuardTests
{
    private static readonly Guid CompanyA = Guid.NewGuid();
    private static readonly Guid CompanyB = Guid.NewGuid();

    private static void Act(bool superAdmin, string[] held, Guid[] companies, Guid target, string key) =>
        PermissionAssignmentGuard.EnsureCanAssign(
            superAdmin, held.ToHashSet(), companies, target, key);

    [Fact]
    public void SuperAdmin_can_assign_anything_anywhere()
    {
        var act = () => Act(superAdmin: true, held: [], companies: [], target: CompanyA, key: "settings.manage");
        act.Should().NotThrow();
    }

    [Fact]
    public void Admin_can_assign_a_permission_they_hold_in_their_company()
    {
        var act = () => Act(false, held: ["ticket.delete"], companies: [CompanyA], target: CompanyA, key: "ticket.delete");
        act.Should().NotThrow();
    }

    [Fact]
    public void Admin_cannot_assign_a_permission_they_do_not_hold()
    {
        var act = () => Act(false, held: ["ticket.view"], companies: [CompanyA], target: CompanyA, key: "ticket.delete");
        act.Should().Throw<ForbiddenException>().Which.Code.Should().Be("permission.assign.not_held");
    }

    [Fact]
    public void Admin_cannot_assign_outside_their_company()
    {
        var act = () => Act(false, held: ["ticket.delete"], companies: [CompanyA], target: CompanyB, key: "ticket.delete");
        act.Should().Throw<ForbiddenException>().Which.Code.Should().Be("permission.assign.out_of_scope");
    }
}
