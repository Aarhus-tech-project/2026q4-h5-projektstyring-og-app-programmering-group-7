/// <reference types="jest" />
import { formatMoney, parseMoney } from "../format";

describe("formatMoney", () => {
  it.each([
    [0, "0,00 kr"],
    [12.5, "12,50 kr"],
    [1234.5, "1.234,50 kr"],
    [1000000, "1.000.000,00 kr"],
    [-70, "-70,00 kr"],
  ])("%p → %s", (amount, expected) => {
    expect(formatMoney(amount)).toBe(expected);
  });
});

describe("parseMoney", () => {
  it.each([
    ["12,95", 12.95],
    ["12.95", 12.95],
    ["3000", 3000],
    ["1.234,50", 1234.5],
    [" 45 kr ", 45],
  ])("%p → %p", (input, expected) => {
    expect(parseMoney(input)).toBe(expected);
  });

  it.each(["", "   ", "abc", "12,999", "-5", "1,2,3"])("rejects %p", (input) => {
    expect(parseMoney(input)).toBeNull();
  });
});
