using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CrmKanban.Api.Middleware;

/// <summary>
/// Automatic FluentValidation pipeline (spec §3): validates every action argument that has a
/// registered validator, before the controller runs. Thrown ValidationException is turned into the
/// error envelope by <see cref="ExceptionHandlingMiddleware"/>. Controllers stay thin.
/// </summary>
public sealed class ValidationFilter(IServiceProvider services) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values.Where(a => a is not null))
        {
            var validatorType = typeof(IValidator<>).MakeGenericType(argument!.GetType());
            if (services.GetService(validatorType) is IValidator validator)
            {
                var result = await validator.ValidateAsync(new ValidationContext<object>(argument));
                if (!result.IsValid)
                    throw new ValidationException(result.Errors);
            }
        }

        await next();
    }
}
