using Api.Data;
using Api.Dtos;
using Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

/// <summary>A household's shared shopping list.</summary>
[Route("api/households/{householdId:int}/shopping")]
public class ShoppingController(AppDbContext db) : HouseholdControllerBase(db)
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ShoppingItemDto>>> List(int householdId)
    {
        if (!await IsMemberAsync(householdId)) return NotFound();
        var items = await Db.ShoppingItems
            .Where(s => s.HouseholdId == householdId)
            .OrderBy(s => s.Checked)
            .ThenBy(s => s.CreatedAt)
            .Select(s => new ShoppingItemDto(s.Id, s.Name, s.Checked, s.CreatedAt))
            .ToListAsync();
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<ShoppingItemDto>> Add(int householdId, CreateShoppingItemRequest request)
    {
        if (!await IsMemberAsync(householdId)) return NotFound();
        var item = new ShoppingItem { HouseholdId = householdId, Name = request.Name.Trim() };
        Db.ShoppingItems.Add(item);
        await Db.SaveChangesAsync();
        return Created($"/api/households/{householdId}/shopping/{item.Id}",
            new ShoppingItemDto(item.Id, item.Name, item.Checked, item.CreatedAt));
    }

    [HttpPatch("{id:int}")]
    public async Task<ActionResult<ShoppingItemDto>> SetChecked(int householdId, int id, UpdateShoppingItemRequest request)
    {
        if (!await IsMemberAsync(householdId)) return NotFound();
        var item = await Db.ShoppingItems.SingleOrDefaultAsync(s => s.HouseholdId == householdId && s.Id == id);
        if (item is null) return NotFound();
        item.Checked = request.Checked;
        await Db.SaveChangesAsync();
        return Ok(new ShoppingItemDto(item.Id, item.Name, item.Checked, item.CreatedAt));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int householdId, int id)
    {
        if (!await IsMemberAsync(householdId)) return NotFound();
        var deleted = await Db.ShoppingItems.Where(s => s.HouseholdId == householdId && s.Id == id).ExecuteDeleteAsync();
        return deleted == 0 ? NotFound() : NoContent();
    }

    /// <summary>Remove everything that's been ticked off.</summary>
    [HttpDelete("checked")]
    public async Task<IActionResult> ClearChecked(int householdId)
    {
        if (!await IsMemberAsync(householdId)) return NotFound();
        await Db.ShoppingItems.Where(s => s.HouseholdId == householdId && s.Checked).ExecuteDeleteAsync();
        return NoContent();
    }
}
