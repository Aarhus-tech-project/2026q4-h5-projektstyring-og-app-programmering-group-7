/// <reference types="jest" />
import type { BudgetStatus } from "../api";
import { budgetCheckMessage, budgetProgress, checkPrice } from "../budget";

function status(monthlyBudget: number | null, spentThisMonth: number): BudgetStatus {
  const remaining = monthlyBudget === null ? null : monthlyBudget - spentThisMonth;
  return {
    monthlyBudget,
    spentThisMonth,
    remaining,
    percentUsed: monthlyBudget ? (spentThisMonth / monthlyBudget) * 100 : null,
    overBudget: remaining !== null && remaining < 0,
    monthStart: "2026-11-01T00:00:00Z",
  };
}

describe("checkPrice", () => {
  it("does nothing without a budget", () => {
    expect(checkPrice(status(null, 500), 100)).toEqual({ kind: "no-budget" });
    expect(checkPrice(null, 100)).toEqual({ kind: "no-budget" });
  });

  it("is ok with plenty left", () => {
    expect(checkPrice(status(1000, 200), 100)).toEqual({ kind: "ok", remainingAfter: 700 });
  });

  it("warns when within 10 % of the budget", () => {
    expect(checkPrice(status(1000, 850), 60)).toEqual({ kind: "near", remainingAfter: 90 });
  });

  it("allows exactly reaching the budget", () => {
    expect(checkPrice(status(500, 450), 50)).toEqual({ kind: "near", remainingAfter: 0 });
  });

  it("flags going over, with the amount", () => {
    expect(checkPrice(status(500, 450), 120)).toEqual({ kind: "over", overBy: 70 });
  });

  it("treats a missing price as 0", () => {
    expect(checkPrice(status(500, 100), null)).toEqual({ kind: "ok", remainingAfter: 400 });
  });
});

describe("budgetCheckMessage", () => {
  it("explains each case", () => {
    expect(budgetCheckMessage({ kind: "no-budget" })).toBeNull();
    expect(budgetCheckMessage({ kind: "over", overBy: 70 })).toBe("This puts you 70,00 kr over this month's budget.");
    expect(budgetCheckMessage({ kind: "near", remainingAfter: 90 })).toContain("Only 90,00 kr left");
  });
});

describe("budgetProgress", () => {
  it("is the share of the budget used, capped at 100 %", () => {
    expect(budgetProgress(status(1000, 250))).toBe(0.25);
    expect(budgetProgress(status(1000, 1800))).toBe(1);
  });

  it("handles a budget of 0", () => {
    expect(budgetProgress(status(0, 0))).toBe(0);
    expect(budgetProgress(status(0, 10))).toBe(1);
  });
});
