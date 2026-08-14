using System.Net;
using System.Text;
using CrmKanban.Infrastructure.Captcha;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrmKanban.Application.Tests.PublicForm;

/// <summary>
/// The bot gate is a security boundary: every path that is not an explicit provider "success" must
/// reject. These tests are the executable spec for that rule (CLAUDE.md — test the core first).
/// </summary>
public class TurnstileCaptchaTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static CaptchaValidator Make(CaptchaOptions opt, HttpMessageHandler? handler = null) =>
        new(new HttpClient(handler ?? new StubHandler(HttpStatusCode.OK, "{\"success\":false}")),
            Options.Create(opt),
            NullLogger<CaptchaValidator>.Instance);

    [Fact]
    public async Task Disabled_gate_lets_everything_through()
    {
        var sut = Make(new CaptchaOptions { Enabled = false });

        (await sut.ValidateAsync(null)).Should().BeTrue();
        sut.SiteKey.Should().BeNull();
    }

    [Fact]
    public async Task Enabled_without_a_secret_fails_closed()
    {
        var sut = Make(new CaptchaOptions { Enabled = true, Provider = "turnstile", SecretKey = null });

        (await sut.ValidateAsync("some-token")).Should().BeFalse();
    }

    [Fact]
    public async Task Enabled_with_an_unknown_provider_fails_closed()
    {
        var sut = Make(new CaptchaOptions { Enabled = true, Provider = "recaptcha", SecretKey = "s3cret" });

        (await sut.ValidateAsync("some-token")).Should().BeFalse();
    }

    [Fact]
    public async Task Missing_token_is_rejected_without_calling_the_provider()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"success\":true}");
        var sut = Make(new CaptchaOptions { Enabled = true, Provider = "turnstile", SecretKey = "s3cret" }, handler);

        (await sut.ValidateAsync("  ")).Should().BeFalse();
        handler.LastRequestBody.Should().BeNull();
    }

    [Fact]
    public async Task Provider_success_passes_and_the_secret_never_leaves_the_server_untouched()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"success\":true,\"challenge_ts\":\"2026-08-14T00:00:00Z\"}");
        var sut = Make(new CaptchaOptions
        {
            Enabled = true, Provider = "turnstile", SecretKey = "s3cret", SiteKey = "0xSITE",
            VerifyUrl = "https://verify.test/siteverify",
        }, handler);

        (await sut.ValidateAsync("browser-token")).Should().BeTrue();
        handler.LastRequestBody.Should().Contain("secret=s3cret").And.Contain("response=browser-token");
        sut.SiteKey.Should().Be("0xSITE");
    }

    [Fact]
    public async Task Provider_failure_is_rejected()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"success\":false,\"error-codes\":[\"invalid-input-response\"]}");
        var sut = Make(new CaptchaOptions { Enabled = true, Provider = "turnstile", SecretKey = "s3cret" }, handler);

        (await sut.ValidateAsync("bad-token")).Should().BeFalse();
    }

    [Fact]
    public async Task Provider_outage_fails_closed()
    {
        var handler = new StubHandler(HttpStatusCode.ServiceUnavailable, "nope");
        var sut = Make(new CaptchaOptions { Enabled = true, Provider = "turnstile", SecretKey = "s3cret" }, handler);

        (await sut.ValidateAsync("good-token")).Should().BeFalse();
    }
}
