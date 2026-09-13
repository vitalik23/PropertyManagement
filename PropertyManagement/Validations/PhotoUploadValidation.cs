namespace PropertyManagement.Validations;

public static class PhotoUploadValidation
{
    public static readonly string[] AllowedContentTypes = ["image/jpeg", "image/png", "image/webp"];

    public const long MaxSizeBytes = 5 * 1024 * 1024;

    public static string? Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return "Please choose a photo to upload.";
        }

        if (file.Length > MaxSizeBytes)
        {
            return "Photo must be smaller than 5 MB.";
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            return "Only JPEG, PNG, or WebP photos are allowed.";
        }

        return null;
    }
}
