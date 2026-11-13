namespace Api.Models;

public class Household
{
    public int Id { get; set; }
    public required string Name { get; set; }

    /// <summary>Code roommates use to join. Expires so a leaked code stops working.</summary>
    public required string InviteCode { get; set; }
    public DateTime InviteExpiresAt { get; set; }

    /// <summary>Optional monthly food budget. New items with a price are checked against it.</summary>
    public decimal? MonthlyBudget { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<HouseholdMember> Members { get; set; } = [];
}

public enum HouseholdRole
{
    Owner,
    Member,
}

public class HouseholdMember
{
    public int HouseholdId { get; set; }
    public Household? Household { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public HouseholdRole Role { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
