namespace PropertyManagement.Infrastructure.Services;

public class BlobStorageOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "property-management-photos";
}
