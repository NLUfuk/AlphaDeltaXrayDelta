using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
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
        services.AddScoped<StatusManagementService>();
        services.AddScoped<CommentService>();

        services.AddScoped<Files.AttachmentService>();
        services.AddScoped<Forms.FormFieldService>();
        services.AddScoped<PublicForm.PublicFormService>();
        services.AddScoped<PublicForm.IntakeTrustService>();
        services.AddScoped<PublicForm.CustomerInviteService>();

        services.AddScoped<Settings.SettingsService>();
        services.AddScoped<Notifications.EmailTemplateService>();
        services.AddScoped<Notifications.NotificationFeedService>();
        services.AddScoped<Reports.ReportService>();
        services.AddScoped<Kvkk.KvkkService>();

        return services;
    }

    /// <summary>
    /// The UI is Turkish-only, and validation errors are now shown to the user verbatim (the SPA joins
    /// the envelope's <c>details</c> instead of printing one generic "check your input" sentence). So the
    /// default English text FluentValidation ships would be user-facing. Two globals fix that for every
    /// rule at once, instead of a .WithMessage() on each of the ~100 rules in this assembly:
    ///   - Culture: FluentValidation ships translated default messages (NotEmpty, EmailAddress, Length…).
    ///   - DisplayNameResolver: "{PropertyName}" otherwise renders the C# member ("NewPassword").
    /// Unknown members fall back to FluentValidation's PascalCase split; a name only needs an entry here
    /// once it actually reaches a user.
    ///
    /// A module initializer, not a line in AddApplication: <c>ValidatorOptions.Global</c> is process-wide
    /// static state that every validator reads, including ones built directly (unit tests) with no DI
    /// container in sight. Hanging it off service registration meant the messages depended on whether
    /// someone had called AddApplication first — the assembly loading is the real precondition.
    /// </summary>
#pragma warning disable CA2255 // Deliberate: see the summary — this configures process-wide static state
                              // that must be set before ANY validator runs, DI-composed or not.
    [ModuleInitializer]
    internal static void ConfigureValidationMessages()
    {
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("tr");
        ValidatorOptions.Global.DisplayNameResolver = (_, member, _) =>
            member is not null && FieldNames.TryGetValue(member.Name, out var tr) ? tr : null;
    }
#pragma warning restore CA2255

    private static readonly Dictionary<string, string> FieldNames = new(StringComparer.Ordinal)
    {
        ["Email"] = "E-posta",
        ["Password"] = "Parola",
        ["NewPassword"] = "Yeni parola",
        ["CurrentPassword"] = "Mevcut parola",
        ["FirstName"] = "Ad",
        ["LastName"] = "Soyad",
        ["Code"] = "Kod",
        ["Token"] = "Bağlantı kodu",
        ["Title"] = "Konu",
        ["Body"] = "Mesaj",
        ["Name"] = "Ad",
        ["Slug"] = "Bağlantı adresi",
        ["Role"] = "Rol",
        ["CompanyId"] = "Şirket",
        ["UserId"] = "Kullanıcı",
        ["PermissionKey"] = "Yetki",
        ["KvkkConsent"] = "KVKK onayı",
        ["Phone"] = "Telefon",
        ["Website"] = "Web sitesi",
        ["Address"] = "Adres",
    };
}
