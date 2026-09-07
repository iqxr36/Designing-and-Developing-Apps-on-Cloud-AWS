using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace CloudMVCApplication.Services;

public sealed class S3FileStorageService : IFileStorageService
{
    public const string StorageRoutePrefix = "/storage/";

    private readonly IAmazonS3 _s3;
    private readonly S3StorageOptions _options;

    public S3FileStorageService(
        IAmazonS3 s3,
        IOptions<S3StorageOptions> options)
    {
        _s3 = s3;
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.BucketName))
        {
            throw new InvalidOperationException(
                "S3Storage:BucketName is not configured.");
        }
    }

    public async Task<string> UploadAsync(
        IFormFile file,
        string folder,
        string storedFileName,
        CancellationToken cancellationToken = default)
    {
        if (file.Length <= 0)
        {
            throw new ArgumentException("The uploaded file is empty.", nameof(file));
        }

        var normalizedFolder = NormalizeFolder(folder);
        var safeFileName = Path.GetFileName(storedFileName);

        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            throw new ArgumentException(
                "A valid stored filename is required.",
                nameof(storedFileName));
        }

        var objectKey = $"{normalizedFolder}/{safeFileName}";

        await using var stream = file.OpenReadStream();

        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            InputStream = stream,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType
        };

        await _s3.PutObjectAsync(request, cancellationToken);

        return $"{StorageRoutePrefix}{objectKey}";
    }

    public Task<GetObjectResponse> DownloadAsync(
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = NormalizeObjectKey(objectKey);

        return _s3.GetObjectAsync(
            _options.BucketName,
            normalizedKey,
            cancellationToken);
    }

    public async Task DeleteAsync(
        string storedPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storedPath) ||
            !storedPath.StartsWith(
                StorageRoutePrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var objectKey = storedPath[StorageRoutePrefix.Length..];
        objectKey = NormalizeObjectKey(objectKey);

        await _s3.DeleteObjectAsync(
            _options.BucketName,
            objectKey,
            cancellationToken);
    }

    private static string NormalizeFolder(string folder)
    {
        var parts = folder
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0 ||
            parts.Any(part => part is "." or ".."))
        {
            throw new ArgumentException(
                "A valid S3 folder is required.",
                nameof(folder));
        }

        return string.Join('/', parts);
    }

    private static string NormalizeObjectKey(string objectKey)
    {
        var normalized = Uri.UnescapeDataString(objectKey)
            .Replace('\\', '/')
            .TrimStart('/');

        var parts = normalized.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0 ||
            parts.Any(part => part is "." or ".."))
        {
            throw new ArgumentException(
                "A valid S3 object key is required.",
                nameof(objectKey));
        }

        return string.Join('/', parts);
    }
}