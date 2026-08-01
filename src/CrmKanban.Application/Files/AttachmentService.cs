using System.Text;
using CrmKanban.Application.Abstractions;
using CrmKanban.Application.Common;
using CrmKanban.Application.Tickets;
using CrmKanban.Domain.Entities;
using CrmKanban.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CrmKanban.Application.Files;

/// <summary>
/// File attachments over private S3-compatible storage (spec §12). Type and size are enforced
/// SERVER-side here — both when presigning an upload and again when linking the object, so a client
/// that skips the presign can't smuggle a disallowed file. Downloads are short-lived presigned GETs,
/// gated by the same per-ticket authorization as the ticket itself; a file on an internal note never
/// reaches a customer (the number-one privacy rule, spec §14/§20).
/// </summary>
public sealed class AttachmentService(
    IAppDbContext db,
    IFileStorage storage,
    TicketAuthorizationService authz,
    IClock clock,
    IOptions<FileOptions> options)
{
    private readonly FileOptions _opt = options.Value;
    private TimeSpan Expiry => TimeSpan.FromMinutes(_opt.PresignExpiryMinutes);

    /// <summary>Validate one file and hand back a presigned PUT under <paramref name="keyPrefix"/>.</summary>
    public UploadUrlResult CreateUploadUrl(string keyPrefix, UploadUrlRequest request)
    {
        ValidateFile(request.ContentType, request.Size);
        var key = $"{keyPrefix.Trim('/')}/{Guid.NewGuid():N}/{Sanitize(request.FileName)}";
        var url = storage.PresignPut(key, request.ContentType, Expiry);
        return new UploadUrlResult(key, url, clock.UtcNow.Add(Expiry));
    }

    /// <summary>Server-side attachment upload for an existing ticket (authenticated staff or the
    /// customer who opened it). The bytes proxy through the API — like the public path — so the browser
    /// never has to reach the storage host, and the real size is measured here instead of trusting the
    /// client. Resolving the actor authorizes the caller's relationship to the ticket first; the file is
    /// stored, linked at ticket level, and the new row is returned.</summary>
    public async Task<AttachmentDto> StoreTicketUploadAsync(
        Guid ticketId, string fileName, string declaredContentType, Stream content, CancellationToken ct = default)
    {
        var ticket = await db.Tickets.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == ticketId && t.DeletedAt == null, ct)
            ?? throw new NotFoundException("ticket.not_found", "Ticket not found.");
        var actor = await authz.ResolveAsync(ticket.CompanyId, ticket.OpenedById, ct);

        using var buffer = await BufferCappedAsync(content, ct);
        var size = buffer.Length;
        // Browsers don't always report a usable MIME (empty/octet-stream, common for Office files), so
        // fall back to one derived from the extension before validating — don't reject a valid file on a
        // missing header.
        var contentType = ResolveContentType(fileName, declaredContentType);
        ValidateFile(contentType, size); // type + real (server-measured) size

        var key = $"{ticket.CompanyId:N}/{ticket.Id:N}/{Guid.NewGuid():N}/{Sanitize(fileName)}";
        buffer.Position = 0;
        await storage.PutAsync(key, buffer, contentType, ct);

        var attachment = new Attachment(ticket.CompanyId, ticket.Id, commentId: null, key, Sanitize(fileName), contentType, size, actor.UserId);
        db.Attachments.Add(attachment);
        // Ticket-level uploads are never on an internal note, so notifying the opener is safe (spec §7,
        // §14). The matrix removes the actor, so a customer attaching to their own ticket isn't self-mailed.
        db.TicketEvents.Add(new TicketEvent(ticket.CompanyId, ticket.Id, actor.UserId, TicketEventType.AttachmentAdded, null, ticket.Number));
        await db.SaveChangesAsync(ct);
        return ToDto(attachment);
    }

    /// <summary>Zero-trust public upload (spec §10): buffer the bytes through the API (capped at the
    /// size limit — the client's declared size is never trusted), inspect extension + content-type +
    /// magic bytes, and only then store. Returns a descriptor the form submit links. Restricted to
    /// pdf/txt/doc/docx; nothing else a customer sends is accepted.</summary>
    public async Task<AttachmentDescriptor> StorePublicUploadAsync(
        string keyPrefix, string fileName, string declaredContentType, Stream content, CancellationToken ct = default)
    {
        using var buffer = await BufferCappedAsync(content, ct);
        var size = buffer.Length;
        var head = buffer.GetBuffer().AsSpan(0, (int)Math.Min(size, 4096));
        PublicFileValidator.Validate(fileName, declaredContentType, head, size, _opt.MaxSizeBytes);

        var storedContentType = PublicFileValidator.CanonicalContentType(fileName);
        var key = $"{keyPrefix.Trim('/')}/{Guid.NewGuid():N}/{Sanitize(fileName)}";
        buffer.Position = 0;
        await storage.PutAsync(key, buffer, storedContentType, ct);
        return new AttachmentDescriptor(key, Sanitize(fileName), storedContentType, size);
    }

    /// <summary>Re-validate a batch a client claims to have uploaded, then materialize the rows.</summary>
    public IReadOnlyList<Attachment> BuildAttachments(
        Guid companyId, Guid ticketId, Guid? commentId,
        IReadOnlyCollection<AttachmentDescriptor> files, Guid uploadedById)
    {
        if (files.Count > _opt.MaxPerAttachTarget)
            throw new BadRequestException("attachment.too_many", $"At most {_opt.MaxPerAttachTarget} files are allowed.");

        return files.Select(f =>
        {
            ValidateFile(f.ContentType, f.Size);
            return new Attachment(companyId, ticketId, commentId, f.Key, f.FileName, f.ContentType, f.Size, uploadedById);
        }).ToList();
    }

    /// <summary>Materialize public-form attachments (spec §10). Defense-in-depth: the bytes were already
    /// inspected at upload; here we re-check count and that each object carries an allowed public
    /// content-type before linking it to the ticket.</summary>
    public IReadOnlyList<Attachment> BuildPublicAttachments(
        Guid companyId, Guid ticketId, IReadOnlyCollection<AttachmentDescriptor> files, Guid uploadedById)
    {
        if (files.Count > _opt.MaxPerAttachTarget)
            throw new BadRequestException("attachment.too_many", $"At most {_opt.MaxPerAttachTarget} files are allowed.");

        return files.Select(f =>
        {
            if (f.Size <= 0 || f.Size > _opt.MaxSizeBytes)
                throw new BadRequestException("attachment.too_large", "File size is out of range.");
            if (!PublicFileValidator.AllowedContentTypes.Contains(f.ContentType))
                throw new BadRequestException("attachment.type_not_allowed", "Only PDF, TXT, DOC and DOCX files are accepted.");
            return new Attachment(companyId, ticketId, commentId: null, f.Key, f.FileName, f.ContentType, f.Size, uploadedById);
        }).ToList();
    }

    /// <summary>Attachment as shown in the ticket detail. The caller has already been authorized for the
    /// ticket; internal-comment attachments must be filtered out for customers by the caller. Url is the
    /// API's own proxy download path (not a presigned storage URL) so it is always same-origin.</summary>
    public static AttachmentDto ToDto(Attachment a) =>
        new(a.Id, a.FileName, a.ContentType, a.Size, $"/api/tickets/attachments/{a.Id}/download");

    /// <summary>Authorized download (spec §12): resolve the caller against the ticket, deny a customer
    /// any file on an internal note, then stream the object back through the API. Caller disposes the
    /// returned stream.</summary>
    public async Task<AttachmentContent> OpenAttachmentAsync(Guid attachmentId, CancellationToken ct = default)
    {
        var attachment = await db.Attachments.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == attachmentId && a.DeletedAt == null, ct)
            ?? throw new NotFoundException("attachment.not_found", "Attachment not found.");

        var ticket = await db.Tickets.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == attachment.TicketId && t.DeletedAt == null, ct)
            ?? throw new NotFoundException("ticket.not_found", "Ticket not found.");

        var actor = await authz.ResolveAsync(ticket.CompanyId, ticket.OpenedById, ct);

        if (!actor.IsStaff && attachment.CommentId is { } commentId)
        {
            var isInternal = await db.Comments.IgnoreQueryFilters()
                .AnyAsync(c => c.Id == commentId && c.IsInternal, ct);
            if (isInternal)
                throw new ForbiddenException("attachment.internal_forbidden", "This attachment is on an internal note.");
        }

        var stream = await storage.GetAsync(attachment.S3Key, ct);
        return new AttachmentContent(stream, attachment.ContentType, attachment.FileName);
    }

    /// <summary>Buffer a stream into memory, reading one byte past the size limit so an oversize file is
    /// detected (the client's declared size is never trusted). Shared by the public and staff paths.</summary>
    private async Task<MemoryStream> BufferCappedAsync(Stream content, CancellationToken ct)
    {
        var buffer = new MemoryStream();
        var cap = _opt.MaxSizeBytes + 1;
        var chunk = new byte[81920];
        int read;
        while (buffer.Length < cap && (read = await content.ReadAsync(chunk.AsMemory(), ct)) > 0)
            buffer.Write(chunk, 0, read);
        return buffer;
    }

    // Extension → canonical content-type for the allowed set, used only when the client doesn't send a
    // usable one. Keep in sync with FileOptions.AllowedContentTypes.
    private static readonly Dictionary<string, string> ContentTypeByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    };

    private string ResolveContentType(string fileName, string declaredContentType)
    {
        if (!string.IsNullOrWhiteSpace(declaredContentType)
            && !declaredContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase)
            && _opt.AllowedContentTypes.Contains(declaredContentType, StringComparer.OrdinalIgnoreCase))
            return declaredContentType;
        var ext = System.IO.Path.GetExtension(fileName);
        return ContentTypeByExtension.TryGetValue(ext, out var mapped) ? mapped : declaredContentType;
    }

    private void ValidateFile(string contentType, long size)
    {
        if (size <= 0 || size > _opt.MaxSizeBytes)
            throw new BadRequestException("attachment.too_large", $"File size must be between 1 byte and {_opt.MaxSizeBytes} bytes.");
        if (!_opt.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            throw new BadRequestException("attachment.type_not_allowed", $"Content type '{contentType}' is not allowed.");
    }

    private static string Sanitize(string fileName)
    {
        var name = System.IO.Path.GetFileName(fileName).Trim();
        if (name.Length == 0) return "file";
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name)
            sb.Append(char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' ? ch : '_');
        return sb.ToString();
    }
}
