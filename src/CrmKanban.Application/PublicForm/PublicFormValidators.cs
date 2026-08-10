using FluentValidation;

namespace CrmKanban.Application.PublicForm;

public sealed class PublicFormSubmitRequestValidator : AbstractValidator<PublicFormSubmitRequest>
{
    public PublicFormSubmitRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(10000);
        // KVKK consent is a hard gate (spec §16); a false value is a validation error, not a silent skip.
        RuleFor(x => x.KvkkConsent).Equal(true).WithMessage("Devam etmek için KVKK aydınlatma metnini onaylamalısınız.");
    }
}

public sealed class CustomerFormSubmitRequestValidator : AbstractValidator<CustomerFormSubmitRequest>
{
    public CustomerFormSubmitRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(10000);
    }
}

/// <summary>Staff minting a customer link. The email is the only required field — it is what the
/// token binds to; the name merely prefills the sign-up form (Faz 35).</summary>
public sealed class CustomerInviteRequestValidator : AbstractValidator<CustomerInviteRequest>
{
    public CustomerInviteRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FirstName).MaximumLength(100);
        RuleFor(x => x.LastName).MaximumLength(100);
    }
}
