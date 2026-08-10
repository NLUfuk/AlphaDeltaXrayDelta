using FluentValidation;

namespace CrmKanban.Application.Auth;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
    }
}

public sealed class CustomerRegisterRequestValidator : AbstractValidator<CustomerRegisterRequest>
{
    public CustomerRegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).StrongPassword();
    }
}

public sealed class VerifyCodeRequestValidator : AbstractValidator<VerifyCodeRequest>
{
    public VerifyCodeRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Code).NotEmpty().Matches("^[0-9]{6}$").WithMessage("Kod 6 haneli olmalı.");
    }
}

/// <summary>
/// The one definition of "strong enough" for every place a password is set: staff invite acceptance,
/// password reset, self-service customer sign-up, and change-password. The frontend mirrors these four
/// rules (frontend/src/lib/messages.ts <c>passwordProblem</c>) so the user is told BEFORE submitting;
/// this stays the authority — a client that skips the hint still gets rejected here.
/// </summary>
internal static class PasswordRules
{
    /// <summary>
    /// The special-character set, spelled out rather than written as "not a letter or a digit".
    /// `[^A-Za-z0-9]` would have counted Turkish letters (ş, ğ, ı, ö, ç, ü) as special — so "Parolaş1"
    /// would silently satisfy a rule the user was told required a special character. An explicit set of
    /// ASCII punctuation and symbols means the hint and the check agree. Space is deliberately not in it.
    /// Must stay identical to the frontend mirror in frontend/src/lib/messages.ts.
    /// </summary>
    private const string SpecialCharacters = @"[!@#$%^&*()\-_=+\[\]{};:'"",.<>/?\\|`~]";

    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Parola gerekli.")
            .MinimumLength(8).WithMessage("Parola en az 8 karakter olmalı.")
            .Matches("[A-Z]").WithMessage("Parola en az bir büyük harf içermeli.")
            .Matches("[a-z]").WithMessage("Parola en az bir küçük harf içermeli.")
            .Matches("[0-9]").WithMessage("Parola en az bir rakam içermeli.")
            .Matches(SpecialCharacters).WithMessage(@"Parola en az bir özel karakter içermeli (örn. ! @ # $ % & * ? _ -).");
}

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).StrongPassword().NotEqual(x => x.CurrentPassword)
            .WithMessage("Yeni parola mevcut parolayla aynı olamaz.");
    }
}

public sealed class AcceptInviteRequestValidator : AbstractValidator<AcceptInviteRequest>
{
    public AcceptInviteRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).StrongPassword();
    }
}

public sealed class InviteUserRequestValidator : AbstractValidator<InviteUserRequest>
{
    public InviteUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CompanyId).NotEmpty();
        RuleFor(x => x.Role).IsInEnum();
    }
}

public sealed class AssignPermissionRequestValidator : AbstractValidator<AssignPermissionRequest>
{
    public AssignPermissionRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.CompanyId).NotEmpty();
        RuleFor(x => x.PermissionKey).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
    }
}
