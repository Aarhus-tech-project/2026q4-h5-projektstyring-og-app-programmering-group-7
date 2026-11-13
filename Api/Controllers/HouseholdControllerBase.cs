using System.Security.Claims;
using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

/// <summary>
/// Shared authorization for everything inside a household: the caller must be a member.
/// Non-members get 404 (not 403), so they can't even tell whether a household id exists.
/// </summary>
[ApiController]
[Authorize]
public abstract class HouseholdControllerBase : ControllerBase
{
    protected readonly AppDbContext Db;

    protected HouseholdControllerBase(AppDbContext db) => Db = db;

    protected int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    protected Task<HouseholdMember?> GetMembershipAsync(int householdId) =>
        Db.HouseholdMembers.SingleOrDefaultAsync(m => m.HouseholdId == householdId && m.UserId == UserId);

    protected Task<bool> IsMemberAsync(int householdId) =>
        Db.HouseholdMembers.AnyAsync(m => m.HouseholdId == householdId && m.UserId == UserId);
}
