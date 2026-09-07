using System.Net;
using Amazon.S3;
using CloudMVCApplication.Services;
using Microsoft.Extensions.Options;

namespace CloudMVCApplication.Services;

/// <summary>
/// Serves legacy /uploads/* URLs from the private S3 bucket when the object exists,
/// otherwise falls through to wwwroot static files.
/// </summary>
public sealed class LegacyUploadsS3Middleware
{
    private readonly RequestDelegate _next;

    public LegacyUploadsS3Middleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IAmazonS3 s3,
        IOptions<S3StorageOptions> options)
    {
        var path = context.Request.Path.Value;
        var isRead = HttpMethods.IsGet(context.Request.Method) ||
                     HttpMethods.IsHead(context.Request.Method);

        if (string.IsNullOrWhiteSpace(path) ||
            !path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) ||
            !isRead)
        {
            await _next(context);
            return;
        }

        var relativeKey = path["/uploads/".Length..].TrimStart('/');
        if (string.IsNullOrWhiteSpace(relativeKey) ||
            relativeKey.Contains("..", StringComparison.Ordinal))
        {
            await _next(context);
            return;
        }

        var bucket = options.Value.BucketName;
        if (string.IsNullOrWhiteSpace(bucket))
        {
            await _next(context);
            return;
        }

        try
        {
            var response = await s3.GetObjectAsync(
                bucket,
                relativeKey,
                context.RequestAborted);

            context.Response.RegisterForDispose(response);

            var contentType = string.IsNullOrWhiteSpace(response.Headers.ContentType)
                ? "application/octet-stream"
                : response.Headers.ContentType;

            context.Response.ContentType = contentType;
            context.Response.StatusCode = StatusCodes.Status200OK;

            if (HttpMethods.IsHead(context.Request.Method))
            {
                context.Response.ContentLength = response.Headers.ContentLength;
                return;
            }

            await response.ResponseStream.CopyToAsync(
                context.Response.Body,
                context.RequestAborted);
        }
        catch (AmazonS3Exception exception)
            when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            await _next(context);
        }
    }
}
