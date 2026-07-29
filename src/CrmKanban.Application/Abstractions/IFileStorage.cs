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
}
