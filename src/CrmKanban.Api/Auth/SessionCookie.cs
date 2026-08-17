using CrmKanban.Application.Auth;

namespace CrmKanban.Api.Auth;

/// <summary>
/// Where the session actually lives (spec §3, §9). The refresh token is written to an httpOnly cookie
/// and never appears in a response body; the SPA receives only the short-lived access token and keeps
/// it in memory. Before this, both tokens sat in localStorage, so any script that ran on the page —
/// and anyone who opened DevTools — could read a 14-day session that survived a browser restart. Now
/// the worst an injected script can take is the minutes left on one access token, and the refresh
/// token is unreachable from JavaScript by construction rather than by convention.
/// </summary>
public static class SessionCookie
{
    public const string Name = "crm.rt";

    /// <summary>While a super admin acts as someone else, their own refresh token is parked here so
    /// <c>POST /api/auth/stop-impersonation</c> can hand it back. It used to be snapshotted in
    /// localStorage by the browser, which meant the real admin's session was the most valuable thing
    /// on the page during exactly the operation that touches other people's data.</summary>
    public const string OriginalName = "crm.rt.orig";

    /// <summary>Deliberately NOT httpOnly: the SPA has to know it is inside an impersonated session to
    /// draw the "return to your account" strip, and after a reload it has no other way to find out (the
    /// tokens are unreadable to it now, by design). It carries no secret — only the fact that
    /// <see cref="OriginalName"/> exists — so forging it buys an attacker a banner and a 400.</summary>
    public const string ImpersonationMarker = "crm.imp";

    /// <summary>Writes the refresh token cookie and returns the body the SPA gets — access token and
    /// user only. Every session-issuing endpoint goes through here so none of them can forget.</summary>
    public static SessionResponse Issue(HttpContext ctx, AuthResult result, TimeSpan refreshLifetime)
    {
        ctx.Response.Cookies.Append(Name, result.RefreshToken, Options(ctx, refreshLifetime));
        return new SessionResponse(result.AccessToken, result.AccessTokenExpiresAt, result.User);
    }

    /// <summary>Moves the caller's current refresh cookie aside before an impersonated session
    /// overwrites it. No cookie (session already expired) is not an error — the admin simply cannot
    /// return, and <c>stop-impersonation</c> says so.</summary>
    public static void SnapshotForImpersonation(HttpContext ctx, TimeSpan refreshLifetime)
    {
        if (!ctx.Request.Cookies.TryGetValue(Name, out var current) || string.IsNullOrEmpty(current))
            return;
        ctx.Response.Cookies.Append(OriginalName, current, Options(ctx, refreshLifetime));
        ctx.Response.Cookies.Append(ImpersonationMarker, "1", MarkerOptions(ctx, refreshLifetime));
    }

    public static string? Read(HttpContext ctx) =>
        ctx.Request.Cookies.TryGetValue(Name, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

    public static string? ReadOriginal(HttpContext ctx) =>
        ctx.Request.Cookies.TryGetValue(OriginalName, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

    public static void Clear(HttpContext ctx)
    {
        // Delete must repeat the attributes the cookie was written with — a Set-Cookie whose Path does
        // not match leaves the original in place and the "logout" only looks like one.
        ctx.Response.Cookies.Delete(Name, Options(ctx, TimeSpan.Zero));
        ctx.Response.Cookies.Delete(OriginalName, Options(ctx, TimeSpan.Zero));
        ctx.Response.Cookies.Delete(ImpersonationMarker, MarkerOptions(ctx, TimeSpan.Zero));
    }

    public static void ClearOriginal(HttpContext ctx)
    {
        ctx.Response.Cookies.Delete(OriginalName, Options(ctx, TimeSpan.Zero));
        ctx.Response.Cookies.Delete(ImpersonationMarker, MarkerOptions(ctx, TimeSpan.Zero));
    }

    // The marker is readable by the SPA and needed on every page, so it differs from the token cookies
    // in exactly two ways: HttpOnly off and Path at the root. Everything else stays identical.
    private static CookieOptions MarkerOptions(HttpContext ctx, TimeSpan lifetime)
    {
        var o = Options(ctx, lifetime);
        o.HttpOnly = false;
        o.Path = "/";
        return o;
    }

    private static CookieOptions Options(HttpContext ctx, TimeSpan lifetime) => new()
    {
        HttpOnly = true,
        // Live traffic is HTTPS-only (Faz 34 forces the redirect), so this is true in production. Keying
        // it off the request rather than hardcoding true keeps the http://localhost dev loop working
        // without a second, weaker code path that could ship by accident.
        Secure = ctx.Request.IsHttps,
        // The SPA is same-origin with the API, so no legitimate cross-site request ever needs this
        // cookie — Strict, and CSRF against the refresh endpoint stops being reachable.
        SameSite = SameSiteMode.Strict,
        // Scoped to the only endpoints that consume it: it is not attached to the other ~40 /api calls.
        Path = "/api/auth",
        Expires = lifetime == TimeSpan.Zero ? null : DateTimeOffset.UtcNow.Add(lifetime),
    };
}

/// <summary>What a session-issuing endpoint returns. Deliberately not <see cref="AuthResult"/>: the
/// refresh token is dropped here, on the way out, so it cannot reach JavaScript.</summary>
public sealed record SessionResponse(string AccessToken, DateTime AccessTokenExpiresAt, UserInfo User);
