using System.Net;
using System.Net.Http.Json;

namespace Api.Tests;

/// <summary>The monthly food budget check when adding items.</summary>
[Collection(ApiCollection.Name)]
public class BudgetTests(ApiFactory factory)
{
    [Fact]
    public async Task Without_a_budget_any_price_is_accepted()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var household = await client.CreateHouseholdAsync(monthlyBudget: null);

        var response = await client.PostItemAsync(household.Id, "Steak", price: 99_999);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var budget = (await response.Content.ReadFromJsonAsync<CreateItemResult>())!.Budget;
        Assert.Null(budget.MonthlyBudget);
        Assert.Equal(99_999, budget.SpentThisMonth);
        Assert.False(budget.OverBudget);
    }

    [Fact]
    public async Task Budget_status_tracks_spending_this_month()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var household = await client.CreateHouseholdAsync(monthlyBudget: 1000);
        await client.AddItemAsync(household.Id, "Milk", price: 150);
        await client.AddItemAsync(household.Id, "Cheese", price: 100);
        await client.AddItemAsync(household.Id, "Salt"); // no price: doesn't count

        var budget = await client.GetFromJsonAsync<BudgetResult>($"/api/households/{household.Id}/budget");

        Assert.Equal(1000, budget!.MonthlyBudget);
        Assert.Equal(250, budget.SpentThisMonth);
        Assert.Equal(750, budget.Remaining);
        Assert.Equal(25, budget.PercentUsed);
        Assert.False(budget.OverBudget);
    }

    [Fact]
    public async Task Item_that_would_exceed_the_budget_is_rejected_with_the_numbers()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var household = await client.CreateHouseholdAsync(monthlyBudget: 500);
        await client.AddItemAsync(household.Id, "Groceries", price: 450);

        var response = await client.PostItemAsync(household.Id, "Wine", price: 120);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OverBudgetResult>();
        Assert.Equal("over_budget", body!.Code);
        Assert.Equal(50, body.Current.Remaining);
        Assert.Equal(-70, body.AfterAdding.Remaining);
        Assert.Contains("70 kr over", body.Message);
        // Nothing was added
        var items = await client.GetFromJsonAsync<List<ItemResult>>($"/api/households/{household.Id}/items");
        Assert.DoesNotContain(items!, i => i.Name == "Wine");
    }

    [Fact]
    public async Task Exactly_reaching_the_budget_is_allowed()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var household = await client.CreateHouseholdAsync(monthlyBudget: 500);
        await client.AddItemAsync(household.Id, "Groceries", price: 450);

        var response = await client.PostItemAsync(household.Id, "Bread", price: 50);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(0, (await response.Content.ReadFromJsonAsync<CreateItemResult>())!.Budget.Remaining);
    }

    [Fact]
    public async Task User_can_confirm_going_over_budget()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var household = await client.CreateHouseholdAsync(monthlyBudget: 100);

        var response = await client.PostItemAsync(household.Id, "Party food", price: 180, allowOverBudget: true);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var budget = (await response.Content.ReadFromJsonAsync<CreateItemResult>())!.Budget;
        Assert.True(budget.OverBudget);
        Assert.Equal(-80, budget.Remaining);
        Assert.Equal(180, budget.PercentUsed);
    }

    [Fact]
    public async Task Items_without_price_are_allowed_even_when_over_budget()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var household = await client.CreateHouseholdAsync(monthlyBudget: 100);
        await client.PostItemAsync(household.Id, "Big shop", price: 150, allowOverBudget: true);

        var response = await client.PostItemAsync(household.Id, "Leftovers");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Budget_is_shared_by_the_whole_household()
    {
        var (owner, _) = await factory.CreateUserClientAsync();
        var (roommate, _) = await factory.CreateUserClientAsync();
        var household = await owner.CreateHouseholdAsync(monthlyBudget: 300);
        await roommate.JoinAsync(household.InviteCode);
        await owner.AddItemAsync(household.Id, "Groceries", price: 250);

        // The roommate's purchase is checked against what the owner already spent
        var response = await roommate.PostItemAsync(household.Id, "Snacks", price: 60);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Removing_the_budget_turns_the_check_off()
    {
        var (owner, _) = await factory.CreateUserClientAsync();
        var household = await owner.CreateHouseholdAsync(monthlyBudget: 10);
        await owner.PutAsJsonAsync($"/api/households/{household.Id}", new { name = household.Name, monthlyBudget = (decimal?)null });

        var response = await owner.PostItemAsync(household.Id, "Groceries", price: 500);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Negative_budget_is_rejected()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var response = await client.PostAsJsonAsync("/api/households", new { name = "Flat", monthlyBudget = -5 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
