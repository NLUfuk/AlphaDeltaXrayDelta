using FluentValidation;

namespace CrmKanban.Application.Companies;

internal sealed class CreateCompanyRequestValidator : AbstractValidator<CreateCompanyRequest>
{
    public CreateCompanyRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        // Slug feeds the public form URL — keep it URL-safe (lowercase letters, digits, hyphens).
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(120)
            .Matches("^[a-z0-9-]+$").WithMessage("Slug yalnızca küçük harf, rakam ve tire içerebilir.");

        // Contact card: length caps match the columns. Deliberately duplicated in the update validator
        // below rather than shared — four MaximumLength calls do not earn an abstraction, and the two
        // sit side by side where a drift is visible. Website is NOT format-checked: people type
        // "www.acme.com", and rejecting that would be a worse bug than storing it (the UI prefixes
        // the scheme when it renders the link).
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.Email).MaximumLength(256).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Website).MaximumLength(300);
        RuleFor(x => x.Address).MaximumLength(500);
    }
}

internal sealed class UpdateCompanyRequestValidator : AbstractValidator<UpdateCompanyRequest>
{
    public UpdateCompanyRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.Email).MaximumLength(256).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Website).MaximumLength(300);
        RuleFor(x => x.Address).MaximumLength(500);
    }
}

internal sealed class DeleteCompanyRequestValidator : AbstractValidator<DeleteCompanyRequest>
{
    public DeleteCompanyRequestValidator() => RuleFor(x => x.ConfirmName).NotEmpty().MaximumLength(200);
}
