using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

[Collection(ApiCollection.Name)]
public class HouseholdTests(ApiFactory factory)
{
    [Fact]
    public async Task Requires_login()
    {
        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/households")).StatusCode);
    }

    [Fact]
    public async Task Creator_becomes_owner_and_gets_an_invite_code()
    {
        var (client, auth) = await factory.CreateUserClientAsync();

        var household = await client.CreateHouseholdAsync("Flat 4B", monthlyBudget: 3000);

        Assert.Equal("Flat 4B", household.Name);
        Assert.Equal("Owner", household.MyRole);
        Assert.Equal(3000, household.MonthlyBudget);
        Assert.Matches("^[A-HJ-NP-Z2-9]{8}$", household.InviteCode); // no 0/O/1/I/L
        Assert.True(household.InviteExpiresAt > DateTime.UtcNow.AddDays(6));
        Assert.Equal(auth.User.Id, Assert.Single(household.Members).UserId);
    }

    [Fact]
    public async Task Roommate_can_join_with_invite_code()
    {
        var (owner, _) = await factory.CreateUserClientAsync();
        var (roommate, _) = await factory.CreateUserClientAsync();
        var household = await owner.CreateHouseholdAsync();

        // Codes are case-insensitive and tolerate spaces/dashes
        var code = household.InviteCode.ToLower().Insert(4, "-");
        var joined = await roommate.JoinAsync(code);

        Assert.Equal(household.Id, joined.Id);
        Assert.Equal("Member", joined.MyRole);
        Assert.Equal(2, joined.Members.Count);
        var mine = await roommate.GetFromJsonAsync<List<HouseholdSummaryResult>>("/api/households");
        Assert.Contains(mine!, h => h.Id == household.Id && h.MemberCount == 2);
    }

    [Fact]
    public async Task Joining_with_wrong_code_returns_404()
    {
        var (client, _) = await factory.CreateUserClientAsync();
        var response = await client.PostAsJsonAsync("/api/households/join", new { inviteCode = "ZZZZZZZZ" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Joining_with_expired_code_returns_404()
    {
        var (owner, _) = await factory.CreateUserClientAsync();
        var (roommate, _) = await factory.CreateUserClientAsync();
        var household = await owner.CreateHouseholdAsync();

        // Move the expiry into the past directly in the database
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Api.Data.AppDbContext>();
            db.Households.Single(h => h.Id == household.Id).InviteExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            db.SaveChanges();
        }

        var response = await roommate.PostAsJsonAsync("/api/households/join", new { inviteCode = household.InviteCode });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Joining_twice_returns_409()
    {
        var (owner, _) = await factory.CreateUserClientAsync();
        var household = await owner.CreateHouseholdAsync();

        var response = await owner.PostAsJsonAsync("/api/households/join", new { inviteCode = household.InviteCode });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Regenerated_invite_code_replaces_the_old_one()
    {
        var (owner, _) = await factory.CreateUserClientAsync();
        var (roommate, _) = await factory.CreateUserClientAsync();
        var household = await owner.CreateHouseholdAsync();

        var response = await owner.PostAsync($"/api/households/{household.Id}/invite-code", null);
        var updated = await response.Content.ReadFromJsonAsync<HouseholdResult>();

        Assert.NotEqual(household.InviteCode, updated!.InviteCode);
        var withOld = await roommate.PostAsJsonAsync("/api/households/join", new { inviteCode = household.InviteCode });
        Assert.Equal(HttpStatusCode.NotFound, withOld.StatusCode);
    }

    [Fact]
    public async Task Only_owner_can_change_settings()
    {
        var (owner, _) = await factory.CreateUserClientAsync();
        var (roommate, _) = await factory.CreateUserClientAsync();
        var household = await owner.CreateHouseholdAsync();
        await roommate.JoinAsync(household.InviteCode);

        var byMember = await roommate.PutAsJsonAsync($"/api/households/{household.Id}", new { name = "Mine now", monthlyBudget = 1 });
        var byOwner = await owner.PutAsJsonAsync($"/api/households/{household.Id}", new { name = "Flat 4B", monthlyBudget = 2500 });

        Assert.Equal(HttpStatusCode.Forbidden, byMember.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byOwner.StatusCode);
        Assert.Equal(2500, (await byOwner.Content.ReadFromJsonAsync<HouseholdResult>())!.MonthlyBudget);
    }

    [Fact]
    public async Task Outsiders_cannot_see_a_household()
    {
        var (owner, _) = await factory.CreateUserClientAsync();
        var (outsider, _) = await factory.CreateUserClientAsync();
        var household = await owner.CreateHouseholdAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/households/{household.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/households/{household.Id}/items")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/households/{household.Id}/shopping")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/households/{household.Id}/budget")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.PostItemAsync(household.Id)).StatusCode);
    }

    [Fact]
    public async Task When_owner_leaves_the_next_member_becomes_owner()
    {
        var (owner, _) = await factory.CreateUserClientAsync();
        var (roommate, _) = await factory.CreateUserClientAsync();
        var household = await owner.CreateHouseholdAsync();
        await roommate.JoinAsync(household.InviteCode);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/households/{household.Id}/members/me")).StatusCode);

        var detail = await roommate.GetFromJsonAsync<HouseholdResult>($"/api/households/{household.Id}");
        Assert.Equal("Owner", detail!.MyRole);
        Assert.Single(detail.Members);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/households/{household.Id}")).StatusCode);
    }

    [Fact]
    public async Task When_last_member_leaves_the_household_is_deleted()
    {
        var (owner, _) = await factory.CreateUserClientAsync();
        var household = await owner.CreateHouseholdAsync();
        await owner.AddItemAsync(household.Id);

        await owner.DeleteAsync($"/api/households/{household.Id}/members/me");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Api.Data.AppDbContext>();
        Assert.False(db.Households.Any(h => h.Id == household.Id));
        Assert.False(db.Items.Any(i => i.HouseholdId == household.Id));
    }
}
