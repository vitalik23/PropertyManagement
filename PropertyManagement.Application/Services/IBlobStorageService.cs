namespace PropertyManagement.Application.Services;

public record BlobUploadResult(string BlobName, string Url);

public interface IBlobStorageService
{
    Task<BlobUploadResult> UploadAsync(string folder, string fileName, Stream content, string contentType, CancellationToken cancellationToken = default);
    Task DeleteAsync(string blobName, CancellationToken cancellationToken = default);
}
