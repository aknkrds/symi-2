using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Symi.Api.DTOs;

namespace Symi.Tests;

public class AuthIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web);

    public AuthIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_ReturnsTokens()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/auth/register", new RegisterRequest("user@example.com", "user1", "Password123!", "User One", new DateOnly(1990,1,1)));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var data = await resp.Content.ReadFromJsonAsync<TokenResponse>(_jsonOpts)!;
        Assert.False(string.IsNullOrWhiteSpace(data.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(data.RefreshToken));
    }

    [Fact]
    public async Task Login_WrongPassword_Unauthorized()
    {
        var client = _factory.CreateClient();
        // Register first
        await client.PostAsJsonAsync("/auth/register", new RegisterRequest("user2@example.com", "user2", "Password123!", null, null));
        var resp = await client.PostAsJsonAsync("/auth/login", new LoginRequest("user2", "wrongpwd"));
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesSingleUse()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/auth/register", new RegisterRequest("user3@example.com", "user3", "Password123!", null, null));
        var login = await client.PostAsJsonAsync("/auth/login", new LoginRequest("user3", "Password123!"));
        var toks = await login.Content.ReadFromJsonAsync<TokenResponse>(_jsonOpts)!;

        var refresh1 = await client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(toks.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refresh1.StatusCode);
        var toks2 = await refresh1.Content.ReadFromJsonAsync<TokenResponse>(_jsonOpts)!;

        // Reuse old refresh should fail
        var refreshReuse = await client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(toks.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshReuse.StatusCode);
    }
}