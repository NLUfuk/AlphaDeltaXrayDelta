namespace CrmKanban.Application.Abstractions;

/// <summary>
/// S3-compatible object storage (spec §3, §12). The bucket is private: uploads and downloads happen
/// through short-lived presigned URLs, never a public URL. One implementation targets both AWS S3 and
/// MinIO — they speak the same API, only the endpoint/credentials differ (config, not code), which is
/// exactly the spec's named variation point, so the seam earns its keep (SCOPE DISCIPLINE).
/// </summary>
public interface IFileStorage
{
    /// <summary>Presigned PUT so the browser uploads bytes directly to storage (spec §12).</summary>
    string PresignPut(string key, string contentType, TimeSpan expiry);

    /// <summary>Short-lived presigned GET for reading a private object (spec §12).</summary>
    string PresignGet(string key, TimeSpan expiry);

    /// <summary>Server-side upload: the bytes flow through the API so they can be inspected before
    /// storage (zero-trust intake, spec §10). Used for both the anonymous public path and the
    /// authenticated staff/customer path so the browser never has to reach the storage host directly.</summary>
    Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>Server-side read: stream a private object back through the API. Downloads proxy through
    /// the API rather than a presigned redirect, so a browser reaches the same origin it already uses
    /// (the storage host may be private/internal, e.g. an in-cluster MinIO).</summary>
    Task<Stream> GetAsync(string key, CancellationToken ct = default);
}
