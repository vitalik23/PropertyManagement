using PropertyManagement.Infrastructure.Services;

namespace PropertyManagement.Tests;

public class FakeBlobStorageService : IBlobStorageService
{
    public List<string> UploadedFolders { get; } = [];
    public List<string> DeletedBlobNames { get; } = [];

    public Task<BlobUploadResult> UploadAsync(string folder, string fileName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        UploadedFolders.Add(folder);
        var blobName = $"{folder}/{Guid.NewGuid()}{Path.GetExtension(fileName)}";
        return Task.FromResult(new BlobUploadResult(blobName, $"https://fake-storage.local/{blobName}"));
    }

    public Task DeleteAsync(string blobName, CancellationToken cancellationToken = default)
    {
        DeletedBlobNames.Add(blobName);
        return Task.CompletedTask;
    }
}
