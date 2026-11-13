using Api.Data;
using Api.Dtos;
using Api.Models;
using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

/// <summary>The shared inventory of one household.</summary>
[Route("api/households/{householdId:int}/items")]
public class ItemsController(AppDbContext db, BudgetService budgets, TimeProvider clock)
    : HouseholdControllerBase(db)
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ItemDto>>> List(
        int householdId,
        [FromQuery] ItemStatus status = ItemStatus.InFridge,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50)
    {
        if (!await IsMemberAsync(householdId)) return NotFound();

        var items = await Db.Items
            .Where(i => i.HouseholdId == householdId && i.Status == status)
            // Soonest expiry first; items without an expiry date last
            .OrderBy(i => i.ExpiryDate == null)
            .ThenBy(i => i.ExpiryDate)
            .ThenBy(i => i.Name)
            .Skip(Math.Max(skip, 0))
            .Take(Math.Clamp(take, 1, 100))
            .Select(i => ToDto(i, i.AddedBy!.Username))
            .ToListAsync();
        return Ok(items);
    }

    /// <summary>
    /// Adds an item. If the household has a monthly food budget and this item's price
    /// would push this month's spending over it, the API answers 409 "over_budget" with
    /// the numbers, unless the client confirms with allowOverBudget=true.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<CreateItemResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<OverBudgetDto>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateItemResponse>> Create(int householdId, CreateItemRequest request)
    {
        if (!await IsMemberAsync(householdId)) return NotFound();

        var budget = await budgets.GetStatusAsync(householdId);
        var price = request.Price ?? 0;
        if (budget.MonthlyBudget is not null && price > 0)
        {
            var after = BudgetService.WithExtra(budget, price);
            if (after.OverBudget && !request.AllowOverBudget)
            {
                return Conflict(new OverBudgetDto(
                    "over_budget",
                    $"Adding this ({price:0.##} kr) puts the household {-after.Remaining!.Value:0.##} kr over this month's food budget of {budget.MonthlyBudget:0.##} kr.",
                    budget,
                    after));
            }
        }

        var item = new Item
        {
            HouseholdId = householdId,
            Name = request.Name.Trim(),
            Barcode = request.Barcode,
            ImageUrl = request.ImageUrl,
            Quantity = request.Quantity,
            ExpiryDate = request.ExpiryDate,
            Price = request.Price,
            AddedByUserId = UserId,
            CreatedAt = clock.GetUtcNow().UtcDateTime,
        };
        Db.Items.Add(item);
        await Db.SaveChangesAsync();

        var username = await Db.Users.Where(u => u.Id == UserId).Select(u => u.Username).SingleAsync();
        var response = new CreateItemResponse(ToDto(item, username), await budgets.GetStatusAsync(householdId));
        return CreatedAtAction(nameof(Get), new { householdId, id = item.Id }, response);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ItemDto>> Get(int householdId, int id)
    {
        if (!await IsMemberAsync(householdId)) return NotFound();
        var item = await FindAsync(householdId, id);
        return item is null ? NotFound() : Ok(ToDto(item, item.AddedBy!.Username));
    }

    [HttpPatch("{id:int}")]
    public async Task<ActionResult<ItemDto>> Update(int householdId, int id, UpdateItemRequest request)
    {
        if (!await IsMemberAsync(householdId)) return NotFound();
        var item = await FindAsync(householdId, id);
        if (item is null) return NotFound();

        if (request.Name is not null) item.Name = request.Name.Trim();
        if (request.Quantity is not null) item.Quantity = request.Quantity.Value;
        if (request.ClearExpiryDate) item.ExpiryDate = null;
        else if (request.ExpiryDate is not null) item.ExpiryDate = request.ExpiryDate;
        await Db.SaveChangesAsync();
        return Ok(ToDto(item, item.AddedBy!.Username));
    }

    /// <summary>Use one unit. When the last unit is used, the item is marked Used.</summary>
    [HttpPost("{id:int}/use")]
    public async Task<ActionResult<ItemStatusChangeResponse>> UseOne(int householdId, int id)
    {
        if (!await IsMemberAsync(householdId)) return NotFound();
        var item = await FindAsync(householdId, id);
        if (item is null) return NotFound();
        if (item.Status != ItemStatus.InFridge) return Conflict(new { message = "Item is no longer in the fridge." });

        item.Quantity--;
        if (item.Quantity <= 0)
        {
            item.Quantity = 0;
            Finish(item, ItemStatus.Used);
        }
        var addedToList = await AddToShoppingListIfOutAsync(item);
        await Db.SaveChangesAsync();
        return Ok(new ItemStatusChangeResponse(ToDto(item, item.AddedBy!.Username), addedToList));
    }

    /// <summary>Throw the whole item out (counts as waste in the stats).</summary>
    [HttpPost("{id:int}/throw-out")]
    public async Task<ActionResult<ItemStatusChangeResponse>> ThrowOut(int householdId, int id)
    {
        if (!await IsMemberAsync(householdId)) return NotFound();
        var item = await FindAsync(householdId, id);
        if (item is null) return NotFound();
        if (item.Status != ItemStatus.InFridge) return Conflict(new { message = "Item is no longer in the fridge." });

        Finish(item, ItemStatus.ThrownOut);
        var addedToList = await AddToShoppingListIfOutAsync(item);
        await Db.SaveChangesAsync();
        return Ok(new ItemStatusChangeResponse(ToDto(item, item.AddedBy!.Username), addedToList));
    }

    /// <summary>Delete a mistaken entry entirely (not counted as used or wasted).</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int householdId, int id)
    {
        if (!await IsMemberAsync(householdId)) return NotFound();
        var deleted = await Db.Items.Where(i => i.HouseholdId == householdId && i.Id == id).ExecuteDeleteAsync();
        return deleted == 0 ? NotFound() : NoContent();
    }

    private Task<Item?> FindAsync(int householdId, int id) =>
        Db.Items.Include(i => i.AddedBy).SingleOrDefaultAsync(i => i.HouseholdId == householdId && i.Id == id);

    private void Finish(Item item, ItemStatus status)
    {
        item.Status = status;
        item.FinishedAt = clock.GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// When an item leaves the fridge and there's no other of the same product left,
    /// put it on the shopping list (unless it's already on it).
    /// </summary>
    private async Task<bool> AddToShoppingListIfOutAsync(Item item)
    {
        if (item.Status == ItemStatus.InFridge) return false;

        var name = item.Name.ToLower();
        var stillHaveSome = await Db.Items.AnyAsync(i =>
            i.HouseholdId == item.HouseholdId && i.Id != item.Id &&
            i.Status == ItemStatus.InFridge && i.Name.ToLower() == name);
        var alreadyOnList = await Db.ShoppingItems.AnyAsync(s =>
            s.HouseholdId == item.HouseholdId && !s.Checked && s.Name.ToLower() == name);
        if (stillHaveSome || alreadyOnList) return false;

        Db.ShoppingItems.Add(new ShoppingItem { HouseholdId = item.HouseholdId, Name = item.Name });
        return true;
    }

    private static ItemDto ToDto(Item i, string addedBy) => new(
        i.Id, i.Name, i.Barcode, i.ImageUrl, i.Quantity, i.ExpiryDate, i.Price, i.Status, addedBy, i.CreatedAt);
}
