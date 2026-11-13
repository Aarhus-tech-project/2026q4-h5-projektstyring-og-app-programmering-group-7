/// <reference types="jest" />
import {
  addDays,
  daysUntil,
  expiryLabel,
  expiryLevel,
  formatDisplayDate,
  isValidIsoDate,
  parseDisplayDate,
  toIsoDate,
} from "../expiry";

// Fixed "today" so tests don't depend on when they run
const today = new Date(2026, 10, 12); // 12 Nov 2026 (months are 0-based)

describe("toIsoDate / addDays", () => {
  it("formats local dates as YYYY-MM-DD", () => {
    expect(toIsoDate(today)).toBe("2026-11-12");
  });

  it("rolls over months and years", () => {
    expect(toIsoDate(addDays(today, 20))).toBe("2026-12-02");
    expect(toIsoDate(addDays(new Date(2026, 11, 31), 1))).toBe("2027-01-01");
  });
});

describe("isValidIsoDate", () => {
  it.each(["2026-11-12", "2028-02-29"])("accepts %s", (d) => expect(isValidIsoDate(d)).toBe(true));
  it.each(["2026-02-30", "2027-02-29", "12-11-2026", "2026-13-01", "tomorrow", ""])("rejects %p", (d) =>
    expect(isValidIsoDate(d)).toBe(false),
  );
});

describe("daysUntil", () => {
  it("counts whole days", () => {
    expect(daysUntil("2026-11-12", today)).toBe(0);
    expect(daysUntil("2026-11-13", today)).toBe(1);
    expect(daysUntil("2026-11-10", today)).toBe(-2);
  });

  it("ignores the time of day", () => {
    const lateEvening = new Date(2026, 10, 12, 23, 59);
    expect(daysUntil("2026-11-13", lateEvening)).toBe(1);
  });

  it("is not thrown off by daylight saving time changes", () => {
    // Clocks change on 29 March 2026 in Denmark
    expect(daysUntil("2026-03-30", new Date(2026, 2, 28))).toBe(2);
  });
});

describe("expiryLevel", () => {
  it.each([
    [null, "none"],
    ["2026-11-11", "expired"],
    ["2026-11-12", "soon"],
    ["2026-11-14", "soon"],
    ["2026-11-15", "ok"],
  ])("%p → %s", (date, level) => {
    expect(expiryLevel(date, today)).toBe(level);
  });
});

describe("expiryLabel", () => {
  it.each([
    [null, "No expiry date"],
    ["2026-11-09", "Expired 3 days ago"],
    ["2026-11-11", "Expired yesterday"],
    ["2026-11-12", "Expires today"],
    ["2026-11-13", "Expires tomorrow"],
    ["2026-11-19", "Expires in 7 days"],
  ])("%p → %s", (date, label) => {
    expect(expiryLabel(date, today)).toBe(label);
  });
});

describe("dd-mm-yyyy dates", () => {
  it("formats API dates as dd-mm-yyyy", () => {
    expect(formatDisplayDate("2026-12-24")).toBe("24-12-2026");
    expect(formatDisplayDate("2027-01-05")).toBe("05-01-2027");
  });

  it.each([
    ["24-12-2026", "2026-12-24"],
    ["24-12-26", "2026-12-24"], // short year still accepted
    ["24/12/2026", "2026-12-24"],
    ["24.12.2026", "2026-12-24"],
    ["1-2-2027", "2027-02-01"],
    [" 05-01-2027 ", "2027-01-05"],
  ])("parses %p → %s", (input, iso) => {
    expect(parseDisplayDate(input)).toBe(iso);
  });

  it.each(["31-02-2026", "24-13-2026", "2026-12-24", "24-12", "tomorrow", ""])("rejects %p", (input) => {
    expect(parseDisplayDate(input)).toBeNull();
  });

  it("round-trips", () => {
    expect(parseDisplayDate(formatDisplayDate("2026-11-12"))).toBe("2026-11-12");
  });
});
