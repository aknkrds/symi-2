using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Symi.Api.DTOs;

namespace Symi.Tests;

public class PolicyAndRateLimitTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web);

    public PolicyAndRateLimitTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AdminPing_NonAdmin_Forbidden()
    {
        var client = _factory.CreateClient();
        var reg = await client.PostAsJsonAsync("/auth/register", new RegisterRequest("na@example.com", "nonadmin", "Password123!", null, null));
        var toks = await reg.Content.ReadFromJsonAsync<TokenResponse>(_jsonOpts)!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", toks.AccessToken);
        var resp = await client.GetAsync("/admin/ping");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task RateLimit_Login_TooManyRequests()
    {
        var client = _factory.CreateClient();
        // Perform 65 login attempts with wrong credentials
        HttpResponseMessage? last = null;
        for (int i = 0; i < 65; i++)
        {
            last = await client.PostAsJsonAsync("/auth/login", new LoginRequest("unknown", "bad"));
        }
        Assert.NotNull(last);
        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.True(last.Headers.Contains("RateLimit-Limit"));
        Assert.True(last.Headers.Contains("RateLimit-Remaining"));
        Assert.True(last.Headers.Contains("RateLimit-Reset"));
    }
}