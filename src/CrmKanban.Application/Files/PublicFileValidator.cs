using CrmKanban.Application.Common;

namespace CrmKanban.Application.Files;

/// <summary>
/// Zero-trust validation for anonymous public uploads (spec §10). A customer may only submit
/// document files — pdf, txt, doc, docx — and NOTHING else. Because the bytes flow through the API
/// (not a direct presigned PUT), we inspect the real content: the extension, the declared
/// content-type, AND the file signature (magic bytes) must all agree. The client is never trusted;
/// a .pdf that isn't really a PDF, or an .exe renamed to .txt, is rejected here.
///
/// This is intentionally stricter than the authenticated attachment allow-list (which also permits
/// images/Office spreadsheets): the public intake surface is the least-trusted one.
/// </summary>
public static class PublicFileValidator
{
    /// <summary>An allowed document kind: its extension, canonical content-type, and a signature test
    /// over the file's leading bytes.</summary>
    private sealed record Kind(string Ext, string ContentType, Func<ReadOnlySpan<byte>, bool> SignatureOk);

    private static readonly Kind[] Allowed =
    [
        new("pdf",  "application/pdf",
            head => StartsWith(head, 0x25, 0x50, 0x44, 0x46, 0x2D)),                 // "%PDF-"
        new("docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            head => StartsWith(head, 0x50, 0x4B, 0x03, 0x04)),                       // ZIP local header "PK\x03\x04"
        new("doc",  "application/msword",
            head => StartsWith(head, 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1)), // OLE2 compound doc
        new("txt",  "text/plain", LooksLikeText),
    ];

    /// <summary>The extensions a customer may upload — surfaced to the UI's accept filter.</summary>
    public static readonly IReadOnlyList<string> AllowedExtensions = [.. Allowed.Select(k => k.Ext)];

    /// <summary>The canonical content-types a stored public object may carry (for the submit-time re-check).</summary>
    public static readonly IReadOnlySet<string> AllowedContentTypes =
        Allowed.Select(k => k.ContentType).ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Validate a public upload against extension, declared content-type, and magic bytes.
    /// Throws <see cref="BadRequestException"/> with a stable code on any mismatch.</summary>
    public static void Validate(string fileName, string declaredContentType, ReadOnlySpan<byte> head, long size, long maxSizeBytes)
    {
        if (size <= 0 || size > maxSizeBytes)
            throw new BadRequestException("attachment.too_large", $"File size must be between 1 byte and {maxSizeBytes} bytes.");

        var ext = System.IO.Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        var kind = Allowed.FirstOrDefault(k => k.Ext == ext)
            ?? throw new BadRequestException("attachment.type_not_allowed",
                "Only PDF, TXT, DOC and DOCX files are accepted.");

        // Declared content-type must match the extension's canonical type (browsers send "text/plain"
        // for .txt, sometimes "application/octet-stream" — accept that fallback only for the extension's sake).
        var declared = declaredContentType.Trim().ToLowerInvariant();
        if (declared != kind.ContentType && declared != "application/octet-stream")
            throw new BadRequestException("attachment.type_mismatch",
                "The file's declared type does not match its extension.");

        if (!kind.SignatureOk(head))
            throw new BadRequestException("attachment.content_mismatch",
                "The file's contents do not match a valid PDF, TXT, DOC or DOCX file.");
    }

    /// <summary>The canonical content-type to store the object under, derived from its extension (the
    /// browser-declared type is not trusted for storage).</summary>
    public static string CanonicalContentType(string fileName)
    {
        var ext = System.IO.Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        return Allowed.First(k => k.Ext == ext).ContentType;
    }

    private static bool StartsWith(ReadOnlySpan<byte> head, params byte[] sig) =>
        head.Length >= sig.Length && head[..sig.Length].SequenceEqual(sig);

    // Text heuristic: a real text file has no NUL bytes and no stray control chars (tab/newline/CR
    // aside). Binaries masquerading as .txt almost always trip this. ponytail: NUL/control sniff, not a
    // full charset decode — upgrade to strict UTF-8 validation if a real edge case shows up.
    private static bool LooksLikeText(ReadOnlySpan<byte> head)
    {
        foreach (var b in head)
        {
            if (b == 0) return false;
            if (b < 0x09 || (b > 0x0D && b < 0x20)) return false;
        }
        return true;
    }
}
