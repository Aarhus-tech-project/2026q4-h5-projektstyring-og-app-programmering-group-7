using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.Tests;

// Client-side copies of the API's JSON shapes
public record MemberResult(int UserId, string Username, string Role, DateTime JoinedAt);
public record HouseholdResult(
    int Id, string Name, string InviteCode, DateTime InviteExpiresAt, decimal? MonthlyBudget, string MyRole,
    List<MemberResult> Members);
public record HouseholdSummaryResult(int Id, string Name, string Role, int MemberCount);
public record BudgetResult(
    decimal? MonthlyBudget, decimal SpentThisMonth, decimal? Remaining, decimal? PercentUsed, bool OverBudget,
    DateTime MonthStart);
public record OverBudgetResult(string Code, string Message, BudgetResult Current, BudgetResult AfterAdding);
public record ItemResult(
    int Id, string Name, string? Barcode, string? ImageUrl, int Quantity, DateOnly? ExpiryDate, decimal? Price,
    string Status, string AddedBy, DateTime CreatedAt);
public record CreateItemResult(ItemResult Item, BudgetResult Budget);
public record StatusChangeResult(ItemResult Item, bool AddedToShoppingList);
public record ShoppingResult(int Id, string Name, bool Checked, DateTime CreatedAt);
public record WasteResult(string Name, int Count);
public record StatsResult(int InFridge, int Used, int ThrownOut, decimal MoneyWasted, List<WasteResult> MostThrownOut,
    BudgetResult Budget);
public record ProductResult(string Barcode, string Name, string? Brand, string? ImageUrl);

public static class TestHelpers
{
    public static async Task<HouseholdResult> CreateHouseholdAsync(
        this HttpClient client, string name = "Flat 4B", decimal? monthlyBudget = null)
    {
        var response = await client.PostAsJsonAsync("/api/households", new { name, monthlyBudget });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<HouseholdResult>())!;
    }

    public static async Task<HouseholdResult> JoinAsync(this HttpClient client, string inviteCode)
    {
        var response = await client.PostAsJsonAsync("/api/households/join", new { inviteCode });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<HouseholdResult>())!;
    }

    public static Task<HttpResponseMessage> PostItemAsync(
        this HttpClient client, int householdId, string name = "Milk", decimal? price = null, int quantity = 1,
        DateOnly? expiryDate = null, bool allowOverBudget = false, string? barcode = null) =>
        client.PostAsJsonAsync($"/api/households/{householdId}/items",
            new { name, price, quantity, expiryDate, allowOverBudget, barcode });

    public static async Task<ItemResult> AddItemAsync(
        this HttpClient client, int householdId, string name = "Milk", decimal? price = null, int quantity = 1,
        DateOnly? expiryDate = null)
    {
        var response = await client.PostItemAsync(householdId, name, price, quantity, expiryDate);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateItemResult>())!.Item;
    }
}
