using Amazon.S3;
using Amazon.S3.Model;
using CrmKanban.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace CrmKanban.Infrastructure.Files;

/// <summary>
/// <see cref="IFileStorage"/> over any S3-compatible endpoint (AWS S3 or MinIO — spec §3, §12).
/// Presigning is a local HMAC operation (no network), so these stay synchronous. The bucket is
/// private; callers only ever receive short-lived presigned URLs.
/// </summary>
public sealed class S3FileStorage(IAmazonS3 client, IOptions<S3Options> options) : IFileStorage
{
    private readonly string _bucket = options.Value.BucketName;

    public string PresignPut(string key, string contentType, TimeSpan expiry) =>
        client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = DateTime.UtcNow.Add(expiry),
        });

    public string PresignGet(string key, TimeSpan expiry) =>
        client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(expiry),
        });

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default) =>
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false,
        }, ct);
}
