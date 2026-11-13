using Api.Data;
using Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Monthly food budget: what a household has spent this calendar month (sum of the
/// prices of items added this month) compared with its budget.
/// </summary>
public class BudgetService(AppDbContext db, TimeProvider clock)
{
    public (DateTime Start, DateTime End) CurrentMonth()
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (start, start.AddMonths(1));
    }

    public async Task<BudgetStatusDto> GetStatusAsync(int householdId)
    {
        var budget = await db.Households
            .Where(h => h.Id == householdId)
            .Select(h => h.MonthlyBudget)
            .SingleAsync();

        var (start, end) = CurrentMonth();
        var spent = await db.Items
            .Where(i => i.HouseholdId == householdId && i.CreatedAt >= start && i.CreatedAt < end)
            .SumAsync(i => i.Price ?? 0);

        return BuildStatus(budget, spent, start);
    }

    public static BudgetStatusDto BuildStatus(decimal? budget, decimal spent, DateTime monthStart)
    {
        if (budget is null)
            return new BudgetStatusDto(null, spent, null, null, false, monthStart);

        var remaining = budget.Value - spent;
        var percentUsed = budget.Value == 0 ? 100 : Math.Round(spent / budget.Value * 100, 1);
        return new BudgetStatusDto(budget, spent, remaining, percentUsed, remaining < 0, monthStart);
    }

    /// <summary>The budget status as it would be after spending <paramref name="price"/> more.</summary>
    public static BudgetStatusDto WithExtra(BudgetStatusDto status, decimal price) =>
        BuildStatus(status.MonthlyBudget, status.SpentThisMonth + price, status.MonthStart);
}
