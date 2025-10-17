using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Symi.Api.DTOs;

namespace Symi.Tests;

public class ProfileIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web);

    public ProfileIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient client, string accessToken)> CreateAuthedClient(string email, string username)
    {
        var client = _factory.CreateClient();
        var reg = await client.PostAsJsonAsync("/auth/register", new RegisterRequest(email, username, "Password123!", null, null));
        var toks = await reg.Content.ReadFromJsonAsync<TokenResponse>(_jsonOpts)!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", toks.AccessToken);
        return (client, toks.AccessToken);
    }

    [Fact]
    public async Task Me_ReturnsProfile()
    {
        var (client, access) = await CreateAuthedClient("p1@example.com", "p1");
        var resp = await client.GetAsync("/me");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var me = await resp.Content.ReadFromJsonAsync<MeResponse>(_jsonOpts)!;
        Assert.Equal("p1", me.Username);
    }

    [Fact]
    public async Task Update_UsernameConflict_409()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/auth/register", new RegisterRequest("u1@example.com", "u1", "Password123!", null, null));
        var reg2 = await client.PostAsJsonAsync("/auth/register", new RegisterRequest("u2@example.com", "u2", "Password123!", null, null));
        var toks2 = await reg2.Content.ReadFromJsonAsync<TokenResponse>(_jsonOpts)!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", toks2.AccessToken);

        var resp = await client.PutAsJsonAsync("/me", new UpdateProfileRequest { Username = "u1" });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }
}