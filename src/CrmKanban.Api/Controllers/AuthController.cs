using CrmKanban.Api.Auth;
using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Auth;
using CrmKanban.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace CrmKanban.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    AuthService auth,
    ICurrentUserService currentUser,
    IOptions<AuthOptions> authOptions) : ControllerBase
{
    private TimeSpan RefreshLifetime => TimeSpan.FromDays(authOptions.Value.RefreshTokenDays);

    /// <summary>Issues the session: refresh token into the httpOnly cookie, access token into the body.
    /// See <see cref="SessionCookie"/> for why the refresh token never reaches JavaScript.</summary>
    private SessionResponse Issue(AuthResult result) => SessionCookie.Issue(HttpContext, result, RefreshLifetime);

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<ActionResult<SessionResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(Issue(await auth.LoginAsync(request, ct)));

    /// <summary>Self-service customer registration (spec §18.5). Rate-limited like the public form.
    /// Always 204 (no enumeration): the caller is told to check their email regardless.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("public-form")]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        await auth.RegisterAsync(request, ct);
        return NoContent();
    }

    /// <summary>Self-service password reset (spec §1.12). Rate-limited; always 204 (no enumeration).
    /// The reset link is emailed and reuses the /invite set-password page.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("public-form")]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken ct)
    {
        await auth.ForgotPasswordAsync(request, ct);
        return NoContent();
    }

    /// <summary>Exchanges the refresh cookie for a fresh access token (and rotates the cookie). Takes no
    /// body: the token is read from the cookie the browser attaches, which is the whole point — a body
    /// would mean JavaScript had to be holding it. Also the SPA's boot call, since the access token is
    /// no longer persisted anywhere.</summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<SessionResponse>> Refresh(CancellationToken ct)
    {
        var refreshToken = SessionCookie.Read(HttpContext)
            ?? throw new UnauthorizedException("auth.invalid_refresh", "Invalid refresh token.");
        return Ok(Issue(await auth.RefreshAsync(new RefreshRequest(refreshToken), ct)));
    }

    /// <summary>Revokes the session server-side and clears both cookies. A missing cookie is still a
    /// successful logout — the caller ends up with no session either way.</summary>
    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (SessionCookie.Read(HttpContext) is { } refreshToken)
            await auth.LogoutAsync(new RefreshRequest(refreshToken), ct);
        SessionCookie.Clear(HttpContext);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserInfo>> Me(CancellationToken ct) =>
        Ok(await auth.GetMeAsync(RequireUserId(), ct));

    /// <summary>Super-admin impersonation: returns a session for another user. Gated inside the service
    /// (SuperAdmin-only, no super-admin targets) and audit-logged.</summary>
    [Authorize]
    [HttpPost("impersonate")]
    public async Task<ActionResult<SessionResponse>> Impersonate(ImpersonateRequest request, CancellationToken ct)
    {
        // Park the admin's own refresh token BEFORE the impersonated one overwrites the cookie. Order
        // matters and only works this way round; the service call is what makes the swap irreversible.
        SessionCookie.SnapshotForImpersonation(HttpContext, RefreshLifetime);
        return Ok(Issue(await auth.ImpersonateAsync(request.UserId, ct)));
    }

    /// <summary>Returns the super admin to their own session, from the token parked at impersonation
    /// time. This is server-side now because the browser can no longer hold — and therefore can no
    /// longer restore — a refresh token. The parked token is spent (rotated) and its cookie removed, so
    /// the snapshot cannot be replayed later.</summary>
    [AllowAnonymous]
    [HttpPost("stop-impersonation")]
    public async Task<ActionResult<SessionResponse>> StopImpersonation(CancellationToken ct)
    {
        var original = SessionCookie.ReadOriginal(HttpContext)
            ?? throw new BadRequestException("auth.not_impersonating", "There is no impersonation session to leave.");
        SessionCookie.ClearOriginal(HttpContext);
        return Ok(Issue(await auth.RefreshAsync(new RefreshRequest(original), ct)));
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await auth.ChangePasswordAsync(RequireUserId(), request, ct);
        return NoContent();
    }

    /// <summary>Self-service account deletion (KVKK §16): anonymizes the caller's own account after
    /// password re-confirmation. Not for a super admin (self-orphaning).</summary>
    [Authorize]
    [HttpPost("delete-account")]
    public async Task<IActionResult> DeleteAccount(DeleteAccountRequest request, CancellationToken ct)
    {
        await auth.DeleteOwnAccountAsync(RequireUserId(), request, ct);
        return NoContent();
    }

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new UnauthorizedException("auth.required", "Authentication required.");
}
