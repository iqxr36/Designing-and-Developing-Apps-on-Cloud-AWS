namespace CloudMVCApplication.Services;

public sealed class S3StorageOptions
{
    public const string SectionName = "S3Storage";

    public string BucketName { get; set; } = string.Empty;

    public string Region { get; set; } = "ap-southeast-1";
}