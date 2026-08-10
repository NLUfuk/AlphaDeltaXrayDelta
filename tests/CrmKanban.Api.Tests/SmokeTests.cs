using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace CrmKanban.Api.Tests;

/// <summary>
/// HTTP-level smoke tests (ONERILER P1#14): exercise auth gate + exception envelope + serialization
/// together, end to end, over the real request pipeline.
/// </summary>
public class SmokeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_is_anonymous_and_ok()
    {
        var res = await _client.GetAsync("/health");
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);
    }

    [Fact]
    public async Task A_protected_endpoint_without_a_token_is_401()
    {
        var res = await _client.GetAsync("/api/tickets");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Forgot_password_is_anonymous_and_neutral_204()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "nobody@example.com" });
        res.StatusCode.Should().Be(HttpStatusCode.NoContent, "the endpoint never reveals whether the email exists");
    }

    [Fact]
    public async Task Bad_login_returns_401_with_the_error_envelope()
    {
        var res = await _client.PostAsJsonAsync("/api/auth/login", new { email = "root@test.local", password = "wrong" });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await res.Content.ReadFromJsonAsync<ErrorEnvelope>();
        body!.Code.Should().Be("auth.invalid_credentials");
    }

    [Fact]
    public async Task Super_admin_can_log_in_and_reach_a_protected_endpoint()
    {
        // The startup seed creates the super admin from config (see ApiFactory).
        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email = "root@test.local", password = "Root!TestPass1" });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await login.Content.ReadFromJsonAsync<LoginResponse>();
        auth!.AccessToken.Should().NotBeNullOrEmpty();

        var req = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var me = await _client.SendAsync(req);
        me.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>DELETE with a body is easy to get wrong (binding, validation filter, 204 vs envelope),
    /// so the double confirmation is checked over real HTTP too: the wrong name must not delete.</summary>
    [Fact]
    public async Task Deleting_a_company_needs_the_typed_name_over_http()
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = ApiFactory.SuperAdminEmail, password = ApiFactory.SuperAdminPassword });
        var auth = (await login.Content.ReadFromJsonAsync<LoginResponse>())!;

        async Task<HttpResponseMessage> Send(HttpRequestMessage req)
        {
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);
            return await _client.SendAsync(req);
        }

        var slug = $"smoke-{Guid.NewGuid():N}"[..20];
        var created = await Send(new HttpRequestMessage(HttpMethod.Post, "/api/companies")
        { Content = JsonContent.Create(new { name = "Smoke Co", slug }) });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var company = (await created.Content.ReadFromJsonAsync<CompanyResponse>())!;

        var wrong = await Send(new HttpRequestMessage(HttpMethod.Delete, $"/api/companies/{company.Id}")
        { Content = JsonContent.Create(new { confirmName = "Smoke" }) });
        wrong.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await wrong.Content.ReadFromJsonAsync<ErrorEnvelope>())!.Code.Should().Be("company.delete_name_mismatch");

        var ok = await Send(new HttpRequestMessage(HttpMethod.Delete, $"/api/companies/{company.Id}")
        { Content = JsonContent.Create(new { confirmName = "Smoke Co" }) });
        ok.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await Send(new HttpRequestMessage(HttpMethod.Get, "/api/companies"));
        (await list.Content.ReadFromJsonAsync<CompanyResponse[]>())!.Should().NotContain(c => c.Id == company.Id);
    }

    /// <summary>
    /// The reported bug, over real HTTP: accepting an emailed invite with a password that breaks the
    /// strength rules answered 400 with a body the SPA could not read, so the user was told "sunucuya
    /// ulaşılamadı" instead of what was actually wrong with their password. Asserted here rather than at
    /// the validator, because the failure was in the PIPELINE (which 400 shape comes back), not the rule.
    /// </summary>
    [Fact]
    public async Task A_weak_invite_password_is_400_with_a_readable_turkish_reason()
    {
        var res = await _client.PostAsJsonAsync("/api/invitations/accept",
            new { token = "irrelevant-the-password-is-checked-first", newPassword = "abcdefgh" });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await res.Content.ReadFromJsonAsync<ValidationEnvelope>();
        body!.Code.Should().Be("validation.failed", "the SPA keys its message off `code`; a body without one reads as 'no server'");
        body.Details.Should().NotBeNull().And.NotBeEmpty("the reason must travel — a bare code is what forced the user to guess");
        var reasons = body.Details!.Select(d => d.Error).ToList();
        reasons.Should().Contain("Parola en az bir büyük harf içermeli.");
        reasons.Should().Contain("Parola en az bir rakam içermeli.");
    }

    /// <summary>
    /// [ApiController]'s built-in ProblemDetails 400 bypasses our exception middleware entirely, so a
    /// malformed request used to come back with no `code` at all — the other half of the same symptom.
    /// A missing required field takes that path (model binding fails before any filter runs).
    /// </summary>
    [Fact]
    public async Task A_malformed_request_still_answers_in_the_shared_error_envelope()
    {
        var res = await _client.PostAsJsonAsync("/api/invitations/accept", new { token = (string?)null, newPassword = (string?)null });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await res.Content.ReadFromJsonAsync<ValidationEnvelope>();
        body!.Code.Should().Be("validation.failed");
        body.Details.Should().NotBeNullOrEmpty();
        // ModelState's own text is English serializer detail; it must not reach a user.
        body.Details!.Should().OnlyContain(d => !d.Error.Contains("field is required"));
    }

    private sealed record ErrorEnvelope(string Code, string Message, string? Details);
    private sealed record ValidationEnvelope(string Code, string Message, FieldError[]? Details);
    private sealed record FieldError(string Field, string Error);
    private sealed record LoginResponse(string AccessToken, string RefreshToken);
    private sealed record CompanyResponse(Guid Id, string Name, string Slug);
}
