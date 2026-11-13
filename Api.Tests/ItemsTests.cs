using System.Net;
using System.Net.Http.Json;

namespace Api.Tests;

[Collection(ApiCollection.Name)]
public class ItemsTests(ApiFactory factory)
{
    private static DateOnly InDays(int days) => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(days);

    [Fact]
    public async Task Added_item_is_visible_to_roommates()
    {
        var (owner, auth) = await factory.CreateUserClientAsync();
        var (roommate, _) = await factory.CreateUserClientAsync();
        var household = await owner.CreateHouseholdAsync();
        await roommate.JoinAsync(household.InviteCode);

        await owner.AddItemAsync(household.Id, "Milk", price: 12.95m, quantity: 2, expiryDate: InDays(3));

        var items = await roommate.GetFromJsonAsync<List<ItemResult>>($"/api/households/{household.Id}/items");
        var milk = Assert.Single(items!);
        Assert.Equal("Milk", milk.Name);
        Assert.Equal(2, milk.Quantity);
        Assert.Equal(12.95m, milk.Price);
        Assert.Equal(InDays(3), milk.ExpiryDate);
        Assert.Equal("InFridge", milk.Status);
        Assert.Equal(auth.User.Username, milk.AddedBy);
    }

    [Fact]
    public async Task Inventory_is_sorted_by_soonest_expiry_with_undated_items_last()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var household = await client.CreateHouseholdAsync();
        await client.AddItemAsync(household.Id, "Rice"); // no expiry
        await client.AddItemAsync(household.Id, "Yoghurt", expiryDate: InDays(10));
        await client.AddItemAsync(household.Id, "Milk", expiryDate: InDays(1));

        var items = await client.GetFromJsonAsync<List<ItemResult>>($"/api/households/{household.Id}/items");

        Assert.Equal(["Milk", "Yoghurt", "Rice"], items!.Select(i => i.Name));
    }

    [Fact]
    public async Task Using_the_last_one_moves_it_to_the_shopping_list()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var household = await client.CreateHouseholdAsync();
        var milk = await client.AddItemAsync(household.Id, "Milk", quantity: 2);

        var first = await (await client.PostAsync($"/api/households/{household.Id}/items/{milk.Id}/use", null))
            .Content.ReadFromJsonAsync<StatusChangeResult>();
        var second = await (await client.PostAsync($"/api/households/{household.Id}/items/{milk.Id}/use", null))
            .Content.ReadFromJsonAsync<StatusChangeResult>();

        Assert.Equal(1, first!.Item.Quantity);
        Assert.False(first.AddedToShoppingList);
        Assert.Equal("Used", second!.Item.Status);
        Assert.True(second.AddedToShoppingList);
        var list = await client.GetFromJsonAsync<List<ShoppingResult>>($"/api/households/{household.Id}/shopping");
        Assert.Contains(list!, s => s.Name == "Milk" && !s.Checked);
        // and it's gone from the fridge
        Assert.Empty((await client.GetFromJsonAsync<List<ItemResult>>($"/api/households/{household.Id}/items"))!);
    }

    [Fact]
    public async Task Not_added_to_shopping_list_while_another_one_is_left()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var household = await client.CreateHouseholdAsync();
        var oldMilk = await client.AddItemAsync(household.Id, "Milk");
        await client.AddItemAsync(household.Id, "milk"); // a second carton (case doesn't matter)

        var result = await (await client.PostAsync($"/api/households/{household.Id}/items/{oldMilk.Id}/throw-out", null))
            .Content.ReadFromJsonAsync<StatusChangeResult>();

        Assert.False(result!.AddedToShoppingList);
    }

    [Fact]
    public async Task Not_added_twice_to_the_shopping_list()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var household = await client.CreateHouseholdAsync();
        await client.PostAsJsonAsync($"/api/households/{household.Id}/shopping", new { name = "Eggs" });
        var eggs = await client.AddItemAsync(household.Id, "Eggs");

        await client.PostAsync($"/api/households/{household.Id}/items/{eggs.Id}/use", null);

        var list = await client.GetFromJsonAsync<List<ShoppingResult>>($"/api/households/{household.Id}/shopping");
        Assert.Single(list!, s => s.Name == "Eggs");
    }

    [Fact]
    public async Task Finished_item_cannot_be_used_again()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var household = await client.CreateHouseholdAsync();
        var item = await client.AddItemAsync(household.Id);
        await client.PostAsync($"/api/households/{household.Id}/items/{item.Id}/throw-out", null);

        var again = await client.PostAsync($"/api/households/{household.Id}/items/{item.Id}/use", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Stats_count_waste_and_money_wasted()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var household = await client.CreateHouseholdAsync();
        foreach (var (name, price) in new[] { ("Bread", 20m), ("Bread", 22m), ("Salad", 15m) })
        {
            var item = await client.AddItemAsync(household.Id, name, price);
            await client.PostAsync($"/api/households/{household.Id}/items/{item.Id}/throw-out", null);
        }
        var used = await client.AddItemAsync(household.Id, "Cheese", 40m);
        await client.PostAsync($"/api/households/{household.Id}/items/{used.Id}/use", null);
        await client.AddItemAsync(household.Id, "Butter");

        var stats = await client.GetFromJsonAsync<StatsResult>($"/api/households/{household.Id}/stats");

        Assert.Equal(1, stats!.InFridge);
        Assert.Equal(1, stats.Used);
        Assert.Equal(3, stats.ThrownOut);
        Assert.Equal(57m, stats.MoneyWasted);
        Assert.Equal(new WasteResult("Bread", 2), stats.MostThrownOut[0]);
    }

    [Fact]
    public async Task Item_can_be_edited_and_deleted()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var household = await client.CreateHouseholdAsync();
        var item = await client.AddItemAsync(household.Id, "Mlik", expiryDate: InDays(2));

        var patched = await client.PatchAsJsonAsync($"/api/households/{household.Id}/items/{item.Id}",
            new { name = "Milk", quantity = 3, clearExpiryDate = true });
        var updated = await patched.Content.ReadFromJsonAsync<ItemResult>();
        Assert.Equal("Milk", updated!.Name);
        Assert.Equal(3, updated.Quantity);
        Assert.Null(updated.ExpiryDate);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/households/{household.Id}/items/{item.Id}")).StatusCode);
    }

    [Theory]
    [InlineData("", 1, null)] // empty name
    [InlineData("Milk", 0, null)] // quantity 0
    [InlineData("Milk", 1, "12ab")] // barcode not digits
    public async Task Invalid_item_returns_400(string name, int quantity, string? barcode)
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var household = await client.CreateHouseholdAsync();
        var response = await client.PostItemAsync(household.Id, name, quantity: quantity, barcode: barcode);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cannot_touch_items_of_another_household()
    {
        var (alice, _) = await factory.CreateUserClientAsync();
        var (bob, _) = await factory.CreateUserClientAsync();
        var alicesHome = await alice.CreateHouseholdAsync();
        var bobsHome = await bob.CreateHouseholdAsync();
        var item = await alice.AddItemAsync(alicesHome.Id);

        // Bob uses his own household id but Alice's item id
        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsync($"/api/households/{bobsHome.Id}/items/{item.Id}/use", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/households/{bobsHome.Id}/items/{item.Id}")).StatusCode);
        // and Alice's item is untouched
        var still = await alice.GetFromJsonAsync<ItemResult>($"/api/households/{alicesHome.Id}/items/{item.Id}");
        Assert.Equal("InFridge", still!.Status);
    }
}
