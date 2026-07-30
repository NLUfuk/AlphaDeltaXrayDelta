using System.Reflection;
using CrmKanban.Application.Auth;
using CrmKanban.Application.Authorization;
using CrmKanban.Application.Tickets;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace CrmKanban.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // FluentValidation validators are discovered by assembly scan; the auto pipeline
        // filter (API layer) invokes them.
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), includeInternalTypes: true);

        services.AddScoped<AuthService>();
        services.AddScoped<InvitationService>();
        services.AddScoped<PermissionAssignmentService>();
        services.AddScoped<Authorization.PermissionQueryService>();
        services.AddScoped<Companies.CompanyService>();
        services.AddScoped<Users.UserService>();

        services.AddScoped<TicketAuthorizationService>();
        services.AddScoped<TicketCommandService>();
        services.AddScoped<TicketQueryService>();
        services.AddScoped<CommentService>();

        services.AddScoped<Files.AttachmentService>();
        services.AddScoped<PublicForm.PublicFormService>();

        services.AddScoped<Settings.SettingsService>();
        services.AddScoped<Reports.ReportService>();
        services.AddScoped<Kvkk.KvkkService>();

        return services;
    }
}
