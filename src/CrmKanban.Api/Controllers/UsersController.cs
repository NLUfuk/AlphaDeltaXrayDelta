using CrmKanban.Application.Auth;
using CrmKanban.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrmKanban.Api.Controllers;

/// <summary>Super-admin user management (spec §9): create admin accounts + list users for the RBAC UI.</summary>
[ApiController]
[Authorize]
[Route("api/users")]
public sealed class UsersController(UserService users) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> List([FromQuery] string? search, CancellationToken ct) =>
        Ok(await users.ListAsync(search, ct));

    [HttpPost("admins")]
    public async Task<ActionResult<InviteResult>> CreateAdmin(CreateAdminRequest request, CancellationToken ct) =>
        Ok(await users.CreateAdminAsync(request, ct));
}
