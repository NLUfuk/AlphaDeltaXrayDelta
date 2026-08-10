using CrmKanban.Application.Auth;
using FluentAssertions;

namespace CrmKanban.Application.Tests.Auth;

/// <summary>
/// The password strength rules are the gate on every account that gets born: invite acceptance,
/// password reset, self-service customer sign-up, change-password. They are also what the frontend
/// mirrors (frontend/src/lib/messages.ts passwordProblem) — so a rule that silently changes here
/// desynchronizes the hint the user reads from what the server actually enforces.
///
/// Two invariants are pinned: a weak password is rejected (security), and the rejection carries a
/// Turkish sentence naming the broken rule (the reported bug — the user was shown "sunucuya
/// ulaşılamadı" because nothing ever told them WHICH rule they had broken).
/// </summary>
public class PasswordRuleTests
{
    private static readonly AcceptInviteRequestValidator Validator = new();

    private static IEnumerable<string> ErrorsFor(string password) =>
        Validator.Validate(new AcceptInviteRequest("some-token", password))
            .Errors.Select(e => e.ErrorMessage);

    [Theory]
    [InlineData("", "Parola gerekli.")]
    [InlineData("Ab1!", "Parola en az 8 karakter olmalı.")]
    [InlineData("abcdefg1!", "Parola en az bir büyük harf içermeli.")]
    [InlineData("ABCDEFG1!", "Parola en az bir küçük harf içermeli.")]
    [InlineData("Abcdefgh!", "Parola en az bir rakam içermeli.")]
    [InlineData("Abcdefg1", "Parola en az bir özel karakter içermeli (örn. ! @ # $ % & * ? _ -).")]
    public void A_weak_password_is_rejected_and_says_which_rule_broke(string password, string expected)
    {
        ErrorsFor(password).Should().Contain(expected);
    }

    [Theory]
    [InlineData("Gecerli1!")]
    [InlineData("Gecerli1?")]
    [InlineData("Gecerli1_")]
    [InlineData("Gecerli1-")]
    [InlineData("Gecerli1.")]
    [InlineData("Gecerli1|")]
    [InlineData("Gecerli1\\")]
    [InlineData("Gecerli1\"")]
    [InlineData("Gecerli1'")]
    [InlineData("Gecerli1`")]
    [InlineData("Gecerli1[")]
    public void Every_character_in_the_special_set_actually_satisfies_the_rule(string password)
    {
        // The set is written as one escaped regex character class, where a misplaced backslash silently
        // shrinks it — the user would then be rejected for a symbol the hint told them to use.
        Validator.Validate(new AcceptInviteRequest("some-token", password)).IsValid.Should().BeTrue();
    }

    /// <summary>
    /// `[^A-Za-z0-9]` would accept this: "ş" is not an ASCII letter. In a Turkish app that is a real
    /// input, and accepting it would make the rule disagree with the hint the user just read.
    /// </summary>
    [Fact]
    public void A_turkish_letter_does_not_count_as_a_special_character()
    {
        ErrorsFor("Gecerliş1").Should().Contain("Parola en az bir özel karakter içermeli (örn. ! @ # $ % & * ? _ -).");
    }

    [Fact]
    public void A_missing_token_is_reported_in_turkish_too()
    {
        // The whole point of the fix: nothing user-facing may come back in FluentValidation's default
        // English. This one has no .WithMessage(), so it exercises the global culture/display-name
        // configuration rather than a hand-written string.
        var errors = Validator.Validate(new AcceptInviteRequest("", "Gecerli1!")).Errors;
        errors.Should().ContainSingle();
        errors[0].ErrorMessage.Should().NotContain("must");
        errors[0].ErrorMessage.Should().Contain("Bağlantı kodu");
    }
}
