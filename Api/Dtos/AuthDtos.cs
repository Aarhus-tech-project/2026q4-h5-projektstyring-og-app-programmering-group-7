using System.ComponentModel.DataAnnotations;

namespace Api.Dtos;

public record RegisterRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(3), MaxLength(50)] string Username,
    [Required, MinLength(8), MaxLength(100)] string Password);

public record LoginRequest(
    [Required] string Email,
    [Required] string Password);

public record UserDto(int Id, string Email, string Username);

public record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);
