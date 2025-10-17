using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Symi.Api.DTOs;

namespace Symi.Api.Controllers;

[ApiController]
[Route("media")]
[Authorize]
public class MediaController : ControllerBase
{
    private readonly IAmazonS3 _s3;
    private readonly IConfiguration _config;
    private readonly ILogger<MediaController> _logger;

    public MediaController(IAmazonS3 s3, IConfiguration config, ILogger<MediaController> logger)
    {
        _s3 = s3;
        _config = config;
        _logger = logger;
    }

    [HttpPost("presign")]
    public IActionResult Presign([FromBody] PresignRequest req)
    {
        var allowed = new[] { "image/png", "image/jpeg" };
        if (!allowed.Contains(req.ContentType))
        {
            return BadRequest(new ErrorResponse("invalid_content_type", "Only image/png or image/jpeg allowed"));
        }

        var bucket = _config["S3:Bucket"] ?? "symi-dev-bucket";
        var expires = TimeSpan.FromMinutes(5);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = req.Key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expires),
            ContentType = req.ContentType
        };
        var url = _s3.GetPreSignedURL(request);
        return Ok(new PresignResponse(url, DateTime.UtcNow.Add(expires)));
    }
}