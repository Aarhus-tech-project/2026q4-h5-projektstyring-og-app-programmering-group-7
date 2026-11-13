using System.ComponentModel.DataAnnotations;
using Api.Models;

namespace Api.Dtos;

// --- Households ---

public record CreateHouseholdRequest(
    [Required, MinLength(1), MaxLength(100)] string Name,
    [Range(0, 1_000_000)] decimal? MonthlyBudget);

public record UpdateHouseholdRequest(
    [Required, MinLength(1), MaxLength(100)] string Name,
    [Range(0, 1_000_000)] decimal? MonthlyBudget);

public record JoinHouseholdRequest([Required, MaxLength(20)] string InviteCode);

public record HouseholdSummaryDto(int Id, string Name, HouseholdRole Role, int MemberCount);

public record MemberDto(int UserId, string Username, HouseholdRole Role, DateTime JoinedAt);

public record HouseholdDetailDto(
    int Id,
    string Name,
    string InviteCode,
    DateTime InviteExpiresAt,
    decimal? MonthlyBudget,
    HouseholdRole MyRole,
    IReadOnlyList<MemberDto> Members);

// --- Budget ---

/// <param name="MonthlyBudget">null when the household has no budget.</param>
/// <param name="Remaining">null when there's no budget; negative when over budget.</param>
public record BudgetStatusDto(
    decimal? MonthlyBudget,
    decimal SpentThisMonth,
    decimal? Remaining,
    decimal? PercentUsed,
    bool OverBudget,
    DateTime MonthStart);

/// <summary>409 body when adding an item would exceed the budget (resend with allowOverBudget=true).</summary>
public record OverBudgetDto(string Code, string Message, BudgetStatusDto Current, BudgetStatusDto AfterAdding);

// --- Items ---

public record CreateItemRequest(
    [Required, MinLength(1), MaxLength(200)] string Name,
    [RegularExpression(@"^\d{8,14}$", ErrorMessage = "Barcode must be 8–14 digits.")] string? Barcode,
    [Url, MaxLength(500)] string? ImageUrl,
    [Range(1, 999)] int Quantity,
    DateOnly? ExpiryDate,
    [Range(0, 100_000)] decimal? Price,
    bool AllowOverBudget = false);

public record UpdateItemRequest(
    [MinLength(1), MaxLength(200)] string? Name,
    [Range(1, 999)] int? Quantity,
    DateOnly? ExpiryDate,
    bool ClearExpiryDate = false);

public record ItemDto(
    int Id,
    string Name,
    string? Barcode,
    string? ImageUrl,
    int Quantity,
    DateOnly? ExpiryDate,
    decimal? Price,
    ItemStatus Status,
    string AddedBy,
    DateTime CreatedAt);

public record CreateItemResponse(ItemDto Item, BudgetStatusDto Budget);

/// <param name="AddedToShoppingList">True when this was the last one, so it was put on the shopping list.</param>
public record ItemStatusChangeResponse(ItemDto Item, bool AddedToShoppingList);

// --- Shopping list ---

public record CreateShoppingItemRequest([Required, MinLength(1), MaxLength(200)] string Name);
public record UpdateShoppingItemRequest(bool Checked);
public record ShoppingItemDto(int Id, string Name, bool Checked, DateTime CreatedAt);

// --- Products / stats ---

public record ProductDto(string Barcode, string Name, string? Brand, string? ImageUrl);

public record WasteCountDto(string Name, int Count);

public record HouseholdStatsDto(
    int InFridge,
    int Used,
    int ThrownOut,
    decimal MoneyWasted,
    IReadOnlyList<WasteCountDto> MostThrownOut,
    BudgetStatusDto Budget);
