using Amazon.S3;
using Amazon.S3.Model;

namespace Symi.Api.Services;

public interface IStorageService
{
    string GetPresignedUploadUrl(string bucket, string key, string contentType, TimeSpan? expires = null);
}

public class S3StorageService : IStorageService
{
    private readonly IAmazonS3 _s3;
    public S3StorageService(IAmazonS3 s3)
    {
        _s3 = s3;
    }

    public string GetPresignedUploadUrl(string bucket, string key, string contentType, TimeSpan? expires = null)
    {
        var req = new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expires ?? TimeSpan.FromMinutes(15))
        };
        req.Headers["x-amz-acl"] = "private";
        req.Headers["Content-Type"] = contentType;
        return _s3.GetPreSignedURL(req);
    }
}

public class FakeStorageService : IStorageService
{
    public string GetPresignedUploadUrl(string bucket, string key, string contentType, TimeSpan? expires = null)
    {
        // Testing: return a fake URL; client can still PUT to it in tests if needed
        return $"https://example.local/upload/{Uri.EscapeDataString(bucket)}/{Uri.EscapeDataString(key)}";
    }
}