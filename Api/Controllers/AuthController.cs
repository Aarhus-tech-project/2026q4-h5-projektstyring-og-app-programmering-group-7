using System.Security.Claims;
using Api.Data;
using Api.Dtos;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    AppDbContext db,
    TokenService tokens,
    IPasswordHasher<User> hasher) : ControllerBase
{
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var username = request.Username.Trim();

        if (await db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "Email is already registered." });
        if (await db.Users.AnyAsync(u => u.Username == username))
            return Conflict(new { message = "Username is already taken." });

        var user = new User { Email = email, Username = username };
        user.PasswordHash = hasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return Ok(BuildResponse(user));
    }

    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email);

        if (user is null)
            return Unauthorized(new { message = "Invalid email or password." });

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Invalid email or password." });

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync();
        }

        return Ok(BuildResponse(user));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(sub, out var userId))
            return Unauthorized();

        var user = await db.Users.FindAsync(userId);
        if (user is null)
            return Unauthorized();

        return Ok(ToDto(user));
    }

    private AuthResponse BuildResponse(User user)
    {
        var (token, expiresAt) = tokens.CreateToken(user);
        return new AuthResponse(token, expiresAt, ToDto(user));
    }

    private static UserDto ToDto(User user) => new(user.Id, user.Email, user.Username);
}
