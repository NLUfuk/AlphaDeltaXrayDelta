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

    private sealed record ErrorEnvelope(string Code, string Message, string? Details);
    private sealed record LoginResponse(string AccessToken, string RefreshToken);
}
