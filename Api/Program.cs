using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Api.Data;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// Load secrets from Api/.env into environment variables (must run before the builder reads config).
// Use "__" for nested keys, e.g. ConnectionStrings__Default=...
// NoClobber: real environment variables (hosting, tests) win over the .env file.
DotNetEnv.Env.NoClobber().TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Database (Supabase Postgres)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default must be set (see Api/.env)."),
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", AppDbContext.Schema)));

// Auth
var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
builder.Services.Configure<JwtOptions>(jwtSection);
var jwt = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
if (jwt.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be set and at least 32 characters long.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// FridgeShare services
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<BudgetService>();
builder.Services.AddHttpClient<OpenFoodFactsService>(client =>
{
    client.BaseAddress = new Uri("https://world.openfoodfacts.org/");
    // Open Food Facts asks API users to identify their app in the User-Agent
    client.DefaultRequestHeaders.UserAgent.ParseAdd("FridgeShare/1.0 (school project)");
    client.Timeout = TimeSpan.FromSeconds(8);
});

// Brute-force protection: limit login/register attempts per client IP
var authPermitPerMinute = builder.Configuration.GetValue("RateLimiting:AuthPerMinute", 10);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, ct) => new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(
        new { message = "Too many attempts. Please wait a minute and try again." }, ct));
    options.AddPolicy(RateLimitPolicies.Auth, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermitPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

// Allow the Expo app (web/dev) to call the API
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddControllers()
    // Enums as text ("InFridge") instead of numbers in requests and responses
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

var app = builder.Build();

// Apply any pending EF Core migrations (creates/updates the tables in the "app" schema)
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Swagger UI at /swagger, reading the built-in OpenAPI document
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Api v1");
        options.EnablePersistAuthorization();
    });
}

app.UseHttpsRedirection();

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public static class RateLimitPolicies
{
    public const string Auth = "auth";
}

// Lets the integration tests (Api.Tests) start the app with WebApplicationFactory<Program>
public partial class Program;
