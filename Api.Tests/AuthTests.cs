using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

[Collection(ApiCollection.Name)]
public class AuthTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static object NewUser(string name, string password = "password123") =>
        new { email = $"{name}@example.com", username = name, password };

    private static string Unique() => "u" + Guid.NewGuid().ToString("N")[..10];

    [Fact]
    public async Task Register_returns_token_and_user()
    {
        var name = Unique();
        var response = await _client.PostAsJsonAsync("/api/auth/register", NewUser(name));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResult>();
        Assert.False(string.IsNullOrEmpty(auth!.Token));
        Assert.Equal(name, auth.User.Username);
        Assert.Equal($"{name}@example.com", auth.User.Email);
    }

    [Fact]
    public async Task Register_normalizes_email_to_lowercase()
    {
        var name = Unique();
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { email = $"{name.ToUpper()}@Example.COM", username = name, password = "password123" });

        var auth = await response.Content.ReadFromJsonAsync<AuthResult>();
        Assert.Equal($"{name}@example.com", auth!.User.Email);
    }

    [Fact]
    public async Task Register_with_existing_email_returns_409()
    {
        var name = Unique();
        await _client.PostAsJsonAsync("/api/auth/register", NewUser(name));

        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { email = $"{name}@example.com", username = Unique(), password = "password123" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("not-an-email", "validname", "password123")] // bad email
    [InlineData("ok@example.com", "ab", "password123")] // username too short
    [InlineData("ok@example.com", "validname", "short")] // password too short
    public async Task Register_with_invalid_input_returns_400(string email, string username, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new { email, username, password });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Password_is_stored_hashed_not_in_plain_text()
    {
        var name = Unique();
        await _client.PostAsJsonAsync("/api/auth/register", NewUser(name, "MySecretPass1"));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Api.Data.AppDbContext>();
        var user = db.Users.Single(u => u.Username == name);
        Assert.NotEqual("MySecretPass1", user.PasswordHash);
        Assert.DoesNotContain("MySecretPass1", user.PasswordHash);
    }

    [Fact]
    public async Task Login_with_correct_password_succeeds()
    {
        var name = Unique();
        await _client.PostAsJsonAsync("/api/auth/register", NewUser(name));

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = $"{name}@example.com", password = "password123" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        var name = Unique();
        await _client.PostAsJsonAsync("/api/auth/register", NewUser(name));

        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = $"{name}@example.com", password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_unknown_email_returns_401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "nobody-here@example.com", password = "password123" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_without_token_returns_401()
    {
        var response = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_with_tampered_token_returns_401()
    {
        var (_, auth) = await factory.CreateUserClientAsync();
        var client = factory.CreateClient();
        // Change the last character of the signature
        var tampered = auth.Token[..^1] + (auth.Token[^1] == 'A' ? 'B' : 'A');
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);

        var response = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_with_token_returns_current_user()
    {
        var (client, auth) = await factory.CreateUserClientAsync();

        var me = await client.GetFromJsonAsync<UserResult>("/api/auth/me");

        Assert.Equal(auth.User.Id, me!.Id);
        Assert.Equal(auth.User.Username, me.Username);
    }
}
