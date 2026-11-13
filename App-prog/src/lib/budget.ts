import type { BudgetStatus } from "@/lib/api";
import { formatMoney } from "@/lib/format";

export type BudgetCheck =
  | { kind: "no-budget" }
  | { kind: "ok"; remainingAfter: number }
  | { kind: "near"; remainingAfter: number } // within 10 % of the budget
  | { kind: "over"; overBy: number };

/**
 * Client-side preview of the server's budget check, so the add-item screen can warn
 * while you type the price. The server still does the real check when saving.
 */
export function checkPrice(budget: BudgetStatus | null, price: number | null): BudgetCheck {
  if (!budget || budget.monthlyBudget === null) return { kind: "no-budget" };
  const remainingAfter = budget.monthlyBudget - budget.spentThisMonth - (price ?? 0);
  if (remainingAfter < 0) return { kind: "over", overBy: -remainingAfter };
  if (remainingAfter <= budget.monthlyBudget * 0.1) return { kind: "near", remainingAfter };
  return { kind: "ok", remainingAfter };
}

export function budgetCheckMessage(check: BudgetCheck): string | null {
  switch (check.kind) {
    case "no-budget":
      return null;
    case "ok":
      return `${formatMoney(check.remainingAfter)} left of this month's budget after this.`;
    case "near":
      return `Only ${formatMoney(check.remainingAfter)} left of this month's budget after this.`;
    case "over":
      return `This puts you ${formatMoney(check.overBy)} over this month's budget.`;
  }
}

/** 0–1 for the progress bar (capped at 1). */
export function budgetProgress(budget: BudgetStatus): number {
  if (budget.monthlyBudget === null || budget.monthlyBudget <= 0) return budget.spentThisMonth > 0 ? 1 : 0;
  return Math.min(1, budget.spentThisMonth / budget.monthlyBudget);
}
