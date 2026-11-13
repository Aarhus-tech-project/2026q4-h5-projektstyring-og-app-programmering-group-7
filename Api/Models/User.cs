namespace Api.Models;

public class User
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required string Username { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
