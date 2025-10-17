using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Symi.Api.DTOs;

namespace Symi.Tests;

public class AdminSeedTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web);

    public AdminSeedTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AdminPing_WithSeededAdmin_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var loginResp = await client.PostAsJsonAsync("/auth/login", new LoginRequest("admin", "Admin123!"));
        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
        var toks = await loginResp.Content.ReadFromJsonAsync<TokenResponse>(_jsonOpts)!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", toks.AccessToken);

        var ping = await client.GetAsync("/admin/ping");
        Assert.Equal(HttpStatusCode.OK, ping.StatusCode);
        var body = await ping.Content.ReadAsStringAsync();
        Assert.Contains("\"pong\"", body);
    }
}