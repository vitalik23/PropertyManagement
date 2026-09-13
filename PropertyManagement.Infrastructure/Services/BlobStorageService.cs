using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace PropertyManagement.Infrastructure.Services;

public class BlobStorageService(IOptions<BlobStorageOptions> options) : IBlobStorageService
{
    // Built lazily, on first actual use — not in the constructor. IUnitService/IPropertyService
    // depend on this service for every request (not just photo actions), so eagerly constructing
    // a BlobContainerClient here would crash every page in the app whenever no real Azure Storage
    // connection string is configured yet, instead of only failing when a photo is actually uploaded.
    private readonly Lazy<BlobContainerClient> container = new(() =>
    {
        if (string.IsNullOrWhiteSpace(options.Value.ConnectionString))
        {
            throw new InvalidOperationException(
                "Azure Blob Storage connection string is not configured. Set 'BlobStorage:ConnectionString' via " +
                "dotnet user-secrets (run from the PropertyManagement project directory) before uploading photos.");
        }

        return new BlobContainerClient(options.Value.ConnectionString, options.Value.ContainerName);
    });

    public async Task<BlobUploadResult> UploadAsync(string folder, string fileName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var containerClient = container.Value;
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

        var extension = Path.GetExtension(fileName);
        var blobName = $"{folder}/{Guid.NewGuid()}{extension}";
        var blobClient = containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, cancellationToken);

        return new BlobUploadResult(blobName, blobClient.Uri.ToString());
    }

    public Task DeleteAsync(string blobName, CancellationToken cancellationToken = default)
        => container.Value.DeleteBlobIfExistsAsync(blobName, cancellationToken: cancellationToken);
}
