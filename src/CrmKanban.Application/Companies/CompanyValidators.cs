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
    }
}

internal sealed class DeleteCompanyRequestValidator : AbstractValidator<DeleteCompanyRequest>
{
    public DeleteCompanyRequestValidator() => RuleFor(x => x.ConfirmName).NotEmpty().MaximumLength(200);
}
