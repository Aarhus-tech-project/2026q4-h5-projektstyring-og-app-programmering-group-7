namespace Api.Models;

public enum ItemStatus
{
    InFridge,
    Used,
    ThrownOut,
}

/// <summary>A product in a household's fridge/pantry.</summary>
public class Item
{
    public int Id { get; set; }

    public int HouseholdId { get; set; }
    public Household? Household { get; set; }

    public string? Barcode { get; set; }
    public required string Name { get; set; }
    public string? ImageUrl { get; set; }

    public int Quantity { get; set; } = 1;
    public DateOnly? ExpiryDate { get; set; }

    /// <summary>What was paid for this entry (all units). Counts towards the monthly budget.</summary>
    public decimal? Price { get; set; }

    public int AddedByUserId { get; set; }
    public User? AddedBy { get; set; }

    public ItemStatus Status { get; set; } = ItemStatus.InFridge;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>When it was used up or thrown out.</summary>
    public DateTime? FinishedAt { get; set; }
}

public class ShoppingItem
{
    public int Id { get; set; }

    public int HouseholdId { get; set; }
    public Household? Household { get; set; }

    public required string Name { get; set; }
    public bool Checked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Cached Open Food Facts lookup, so each barcode is only fetched once.</summary>
public class ProductCache
{
    public required string Barcode { get; set; }
    public bool Found { get; set; }
    public string? Name { get; set; }
    public string? Brand { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
}
