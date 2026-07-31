using System.Text.Json;
using CrmKanban.Application.Abstractions;
using CrmKanban.Domain.Entities;

namespace CrmKanban.Application.Auth;

/// <summary>
/// Queues the "set your password / activate your account" email that carries a one-time invite token
/// (spec §9, §14). Both entry points that mint such a token — the public form (new customer) and staff
/// invitation — enqueue through here, so the link shape and the outbox contract live in one place.
/// The row is picked up by the notification worker, rendered against the template, and sent. The raw
/// token only ever leaves the server inside this email, never in an API response.
/// </summary>
public static class InviteEmail
{
    public static void Enqueue(
        IAppDbContext db, string publicBaseUrl, string toEmail, string templateKey,
        string rawToken, IReadOnlyDictionary<string, string> fields)
    {
        var payload = new Dictionary<string, string>(fields)
        {
            ["link"] = $"{publicBaseUrl.TrimEnd('/')}/invite?token={rawToken}",
        };
        db.EmailQueue.Add(new EmailQueue(toEmail, templateKey, JsonSerializer.Serialize(payload)));
    }
}
