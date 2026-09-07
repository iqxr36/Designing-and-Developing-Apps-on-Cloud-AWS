using System.Net;
using Amazon.S3;
using CloudMVCApplication.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudMVCApplication.Controllers;

[Authorize]
[Route("storage")]
public sealed class StorageController : Controller
{
    private readonly IFileStorageService _storage;

    public StorageController(IFileStorageService storage)
    {
        _storage = storage;
    }

    [HttpGet("{**objectKey}")]
    public async Task<IActionResult> GetObject(
        string objectKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return NotFound();
        }

        try
        {
            var response = await _storage.DownloadAsync(
                objectKey,
                cancellationToken);

            HttpContext.Response.RegisterForDispose(response);

            var contentType = string.IsNullOrWhiteSpace(
                response.Headers.ContentType)
                ? "application/octet-stream"
                : response.Headers.ContentType;

            return File(
                response.ResponseStream,
                contentType,
                enableRangeProcessing: true);
        }
        catch (AmazonS3Exception exception)
            when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return NotFound();
        }
    }
}