using System.Text.Json;
using CrmKanban.Application.Common;
using CrmKanban.Domain.Common;
using FluentValidation;

namespace CrmKanban.Api.Middleware;

/// <summary>
/// Single place that turns exceptions into the shared error envelope { code, message, details }
/// (spec §4.3): the frontend has one mapping layer, not a try/catch per component. Domain/app
/// exceptions carry stable codes; unexpected ones become a generic 500 without leaking internals.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await WriteAsync(context, ex);
        }
    }

    private async Task WriteAsync(HttpContext context, Exception ex)
    {
        var (status, code, message, details) = Map(ex);
        if (status >= 500)
            logger.LogError(ex, "Unhandled exception");

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        var payload = JsonSerializer.Serialize(new { code, message, details });
        await context.Response.WriteAsync(payload);
    }

    private static (int Status, string Code, string Message, object? Details) Map(Exception ex) => ex switch
    {
        ValidationException v => (400, "validation.failed", "Validation failed.",
            v.Errors.Select(e => new { field = e.PropertyName, error = e.ErrorMessage })),
        UnauthorizedException e => (401, e.Code, e.Message, null),
        ForbiddenException e => (403, e.Code, e.Message, null),
        NotFoundException e => (404, e.Code, e.Message, null),
        ConflictException e => (409, e.Code, e.Message, null),
        AppException e => (400, e.Code, e.Message, null),
        DomainException e => (422, e.Code, e.Message, null),
        _ => (500, "server.error", "An unexpected error occurred.", null),
    };
}
