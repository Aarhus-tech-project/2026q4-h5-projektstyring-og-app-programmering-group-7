using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Api.Tests;

/// <summary>
/// Runs the real API in memory against a throwaway Postgres container (needs Docker),
/// so tests exercise the same EF Core/Npgsql code paths as production.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17").Build();

    /// <summary>Login/register attempts allowed per minute. High by default so tests don't trip it.</summary>
    protected virtual int AuthPerMinute => 10_000;

    public Task InitializeAsync() => _db.StartAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", _db.GetConnectionString());
        builder.UseSetting("Jwt:Key", "integration-test-signing-key-0123456789-abcdef");
        builder.UseSetting("RateLimiting:AuthPerMinute", AuthPerMinute.ToString());
    }

    /// <summary>Registers a fresh user and returns a client that sends their token.</summary>
    public async Task<(HttpClient Client, AuthResult Auth)> CreateUserClientAsync(string? name = null)
    {
        name ??= "user" + Guid.NewGuid().ToString("N")[..10];
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = $"{name}@example.com", username = name, password = "password123" });
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResult>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return (client, auth);
    }
}

public record UserResult(int Id, string Email, string Username);
public record AuthResult(string Token, DateTime ExpiresAt, UserResult User);

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
