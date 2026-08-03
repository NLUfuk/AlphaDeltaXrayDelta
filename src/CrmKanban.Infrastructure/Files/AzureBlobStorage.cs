using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CrmKanban.Application.Abstractions;

namespace CrmKanban.Infrastructure.Files;

/// <summary>
/// <see cref="IFileStorage"/> over Azure Blob Storage (Microsoft object storage). The container is
/// private; bytes proxy through the API (PutAsync/GetAsync), never a public blob URL. Presigning is the
/// S3 path's browser-direct-upload seam, which this deployment doesn't use (Faz 12 moved every upload
/// and download to API-proxy), so the presign methods are intentionally unsupported.
/// </summary>
public sealed class AzureBlobStorage(BlobContainerClient container) : IFileStorage
{
    public string PresignPut(string key, string contentType, TimeSpan expiry) =>
        throw new NotSupportedException("Azure Blob storage uses API-proxy upload, not presigned URLs.");

    public string PresignGet(string key, TimeSpan expiry) =>
        throw new NotSupportedException("Azure Blob storage uses API-proxy download, not presigned URLs.");

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken ct = default) =>
        await container.GetBlobClient(key).UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
            ct);

    public async Task<Stream> GetAsync(string key, CancellationToken ct = default)
    {
        var response = await container.GetBlobClient(key).DownloadStreamingAsync(cancellationToken: ct);
        return response.Value.Content; // caller disposes; owns the network stream
    }
}
