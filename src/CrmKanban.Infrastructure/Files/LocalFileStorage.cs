using CrmKanban.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace CrmKanban.Infrastructure.Files;

/// <summary>
/// <see cref="IFileStorage"/> over the host's own disk (spec §12 override for single-instance MVP/test
/// deploys, e.g. MonsterASP — no external object store, no cost). Bytes proxy through the API
/// (PutAsync/GetAsync) into a private, non-served folder; there is no public URL, so the presign
/// methods are unsupported (the browser-direct-upload seam the S3 path uses isn't used here — Faz 12).
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<LocalStorageOptions> options)
    {
        var configured = options.Value.RootPath;
        _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "App_Data", "uploads")
            : configured);
        Directory.CreateDirectory(_root);
    }

    public string PresignPut(string key, string contentType, TimeSpan expiry) =>
        throw new NotSupportedException("Local storage uses API-proxy upload, not presigned URLs.");

    public string PresignGet(string key, TimeSpan expiry) =>
        throw new NotSupportedException("Local storage uses API-proxy download, not presigned URLs.");

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, ct);
    }

    public Task<Stream> GetAsync(string key, CancellationToken ct = default)
    {
        var path = ResolvePath(key);
        if (!File.Exists(path))
            throw new FileNotFoundException("Stored object not found.", key);
        return Task.FromResult<Stream>(File.OpenRead(path)); // caller disposes
    }

    // Keys are built server-side from GUIDs + a sanitized filename, but this is a filesystem trust
    // boundary: reject anything that resolves outside the root (path traversal) before touching disk.
    private string ResolvePath(string key)
    {
        var full = Path.GetFullPath(Path.Combine(_root, key.Replace('\\', '/')));
        var rootPrefix = _root.EndsWith(Path.DirectorySeparatorChar) ? _root : _root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootPrefix, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Invalid storage key.");
        return full;
    }
}
