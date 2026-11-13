using Api.Data;
using Api.Dtos;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[Route("api/households")]
public class HouseholdsController(AppDbContext db, BudgetService budgets, TimeProvider clock)
    : HouseholdControllerBase(db)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Households the current user belongs to.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<HouseholdSummaryDto>>> List()
    {
        var households = await Db.HouseholdMembers
            .Where(m => m.UserId == UserId)
            .OrderBy(m => m.JoinedAt)
            .Select(m => new HouseholdSummaryDto(
                m.HouseholdId, m.Household!.Name, m.Role, m.Household.Members.Count))
            .ToListAsync();
        return Ok(households);
    }

    [HttpPost]
    public async Task<ActionResult<HouseholdDetailDto>> Create(CreateHouseholdRequest request)
    {
        var household = new Household
        {
            Name = request.Name.Trim(),
            MonthlyBudget = request.MonthlyBudget,
            InviteCode = await UniqueInviteCodeAsync(),
            InviteExpiresAt = Now + InviteCodes.Lifetime,
            Members = [new HouseholdMember { UserId = UserId, Role = HouseholdRole.Owner }],
        };
        Db.Households.Add(household);
        await Db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = household.Id }, await LoadDetailAsync(household.Id));
    }

    /// <summary>Join with an invite code. Rate limited, so codes can't be brute-forced.</summary>
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost("join")]
    public async Task<ActionResult<HouseholdDetailDto>> Join(JoinHouseholdRequest request)
    {
        var code = InviteCodes.Normalize(request.InviteCode);
        var household = await Db.Households.SingleOrDefaultAsync(h => h.InviteCode == code);

        // Same answer for "wrong" and "expired" so the endpoint doesn't confirm valid codes
        if (household is null || household.InviteExpiresAt < Now)
            return NotFound(new { message = "Invite code is invalid or has expired." });

        if (await IsMemberAsync(household.Id))
            return Conflict(new { message = "You're already a member of this household." });

        Db.HouseholdMembers.Add(new HouseholdMember
        {
            HouseholdId = household.Id, UserId = UserId, Role = HouseholdRole.Member,
        });
        await Db.SaveChangesAsync();
        return Ok(await LoadDetailAsync(household.Id));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<HouseholdDetailDto>> Get(int id)
    {
        if (!await IsMemberAsync(id)) return NotFound();
        return Ok(await LoadDetailAsync(id));
    }

    /// <summary>Rename / set or remove the monthly budget. Owner only.</summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<HouseholdDetailDto>> Update(int id, UpdateHouseholdRequest request)
    {
        var membership = await GetMembershipAsync(id);
        if (membership is null) return NotFound();
        if (membership.Role != HouseholdRole.Owner) return Forbid();

        var household = await Db.Households.SingleAsync(h => h.Id == id);
        household.Name = request.Name.Trim();
        household.MonthlyBudget = request.MonthlyBudget;
        await Db.SaveChangesAsync();
        return Ok(await LoadDetailAsync(id));
    }

    /// <summary>New invite code (old one stops working). Owner only.</summary>
    [HttpPost("{id:int}/invite-code")]
    public async Task<ActionResult<HouseholdDetailDto>> RegenerateInviteCode(int id)
    {
        var membership = await GetMembershipAsync(id);
        if (membership is null) return NotFound();
        if (membership.Role != HouseholdRole.Owner) return Forbid();

        var household = await Db.Households.SingleAsync(h => h.Id == id);
        household.InviteCode = await UniqueInviteCodeAsync();
        household.InviteExpiresAt = Now + InviteCodes.Lifetime;
        await Db.SaveChangesAsync();
        return Ok(await LoadDetailAsync(id));
    }

    /// <summary>
    /// Leave a household. If the owner leaves, the longest-standing member becomes owner;
    /// if the last member leaves, the household and its data are deleted.
    /// </summary>
    [HttpDelete("{id:int}/members/me")]
    public async Task<IActionResult> Leave(int id)
    {
        var membership = await GetMembershipAsync(id);
        if (membership is null) return NotFound();

        Db.HouseholdMembers.Remove(membership);
        var next = await Db.HouseholdMembers
            .Where(m => m.HouseholdId == id && m.UserId != UserId)
            .OrderBy(m => m.JoinedAt)
            .FirstOrDefaultAsync();

        if (next is null)
            Db.Households.Remove(await Db.Households.SingleAsync(h => h.Id == id));
        else if (membership.Role == HouseholdRole.Owner)
            next.Role = HouseholdRole.Owner;

        await Db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:int}/budget")]
    public async Task<ActionResult<BudgetStatusDto>> Budget(int id)
    {
        if (!await IsMemberAsync(id)) return NotFound();
        return Ok(await budgets.GetStatusAsync(id));
    }

    [HttpGet("{id:int}/stats")]
    public async Task<ActionResult<HouseholdStatsDto>> Stats(int id)
    {
        if (!await IsMemberAsync(id)) return NotFound();

        var items = Db.Items.Where(i => i.HouseholdId == id);
        var counts = await items.GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);
        var wasted = await items.Where(i => i.Status == ItemStatus.ThrownOut).SumAsync(i => i.Price ?? 0);
        var mostThrownOut = await items
            .Where(i => i.Status == ItemStatus.ThrownOut)
            .GroupBy(i => i.Name.ToLower())
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new WasteCountDto(g.Max(i => i.Name)!, g.Count()))
            .ToListAsync();

        return Ok(new HouseholdStatsDto(
            counts.GetValueOrDefault(ItemStatus.InFridge),
            counts.GetValueOrDefault(ItemStatus.Used),
            counts.GetValueOrDefault(ItemStatus.ThrownOut),
            wasted,
            mostThrownOut,
            await budgets.GetStatusAsync(id)));
    }

    private async Task<HouseholdDetailDto> LoadDetailAsync(int id)
    {
        var h = await Db.Households.AsNoTracking()
            .Include(x => x.Members).ThenInclude(m => m.User)
            .SingleAsync(x => x.Id == id);
        var members = h.Members
            .OrderBy(m => m.JoinedAt)
            .Select(m => new MemberDto(m.UserId, m.User!.Username, m.Role, m.JoinedAt))
            .ToList();
        var myRole = h.Members.Single(m => m.UserId == UserId).Role;
        return new HouseholdDetailDto(h.Id, h.Name, h.InviteCode, h.InviteExpiresAt, h.MonthlyBudget, myRole, members);
    }

    private async Task<string> UniqueInviteCodeAsync()
    {
        while (true)
        {
            var code = InviteCodes.Generate();
            if (!await Db.Households.AnyAsync(h => h.InviteCode == code)) return code;
        }
    }
}
