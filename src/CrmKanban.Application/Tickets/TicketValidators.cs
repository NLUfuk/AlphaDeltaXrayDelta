using FluentValidation;

namespace CrmKanban.Application.Tickets;

public sealed class CreateTicketRequestValidator : AbstractValidator<CreateTicketRequest>
{
    public CreateTicketRequestValidator()
    {
        RuleFor(x => x.CompanyId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Body).NotEmpty();
        RuleFor(x => x.Priority).IsInEnum();
    }
}

public sealed class EditTicketRequestValidator : AbstractValidator<EditTicketRequest>
{
    public EditTicketRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Body).NotEmpty();
    }
}

public sealed class ChangeStatusRequestValidator : AbstractValidator<ChangeStatusRequest>
{
    public ChangeStatusRequestValidator() => RuleFor(x => x.TargetStatusId).NotEmpty();
}

public sealed class AddCommentRequestValidator : AbstractValidator<AddCommentRequest>
{
    public AddCommentRequestValidator() => RuleFor(x => x.Body).NotEmpty().MaximumLength(10000);
}

public sealed class EditCommentRequestValidator : AbstractValidator<EditCommentRequest>
{
    public EditCommentRequestValidator() => RuleFor(x => x.Body).NotEmpty().MaximumLength(10000);
}
