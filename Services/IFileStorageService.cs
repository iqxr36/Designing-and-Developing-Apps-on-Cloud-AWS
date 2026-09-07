using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;

namespace CloudMVCApplication.Services;

public interface IFileStorageService
{
    Task<string> UploadAsync(
        IFormFile file,
        string folder,
        string storedFileName,
        CancellationToken cancellationToken = default);

    Task<GetObjectResponse> DownloadAsync(
        string objectKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storedPath,
        CancellationToken cancellationToken = default);
}