// Expiry-date logic, kept free of React so it can be unit tested.

/** Local date as "YYYY-MM-DD" (the API's DateOnly format). */
export function toIsoDate(date: Date): string {
  const m = String(date.getMonth() + 1).padStart(2, "0");
  const d = String(date.getDate()).padStart(2, "0");
  return `${date.getFullYear()}-${m}-${d}`;
}

export function addDays(date: Date, days: number): Date {
  const copy = new Date(date);
  copy.setDate(copy.getDate() + days);
  return copy;
}

/** Validates "YYYY-MM-DD" and that it's a real calendar date (no 2026-02-30). */
export function isValidIsoDate(value: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  const [y, m, d] = value.split("-").map(Number);
  const date = new Date(y, m - 1, d);
  return date.getFullYear() === y && date.getMonth() === m - 1 && date.getDate() === d;
}

/** "2026-12-24" (API format) → "24-12-2026" (shown to the user). */
export function formatDisplayDate(isoDate: string): string {
  const [y, m, d] = isoDate.split("-");
  return `${d}-${m}-${y}`;
}

/**
 * Parses what the user typed as day-month-year: "24-12-2026", "24/12/2026", "24.12.2026",
 * "1-2-2027". Two-digit years ("24-12-26") are also accepted and mean 20xx. Returns "YYYY-MM-DD" for the API, or null if
 * it isn't a real date.
 */
export function parseDisplayDate(input: string): string | null {
  const match = input.trim().match(/^(\d{1,2})[-/.](\d{1,2})[-/.](\d{2}|\d{4})$/);
  if (!match) return null;
  const [, d, m, y] = match;
  const year = y.length === 2 ? `20${y}` : y;
  const iso = `${year}-${m.padStart(2, "0")}-${d.padStart(2, "0")}`;
  return isValidIsoDate(iso) ? iso : null;
}

/** Whole days from `today` until the expiry date (negative = expired). */
export function daysUntil(expiryDate: string, today: Date = new Date()): number {
  const [y, m, d] = expiryDate.split("-").map(Number);
  const expiry = Date.UTC(y, m - 1, d);
  const now = Date.UTC(today.getFullYear(), today.getMonth(), today.getDate());
  return Math.round((expiry - now) / 86_400_000);
}

export type ExpiryLevel = "expired" | "soon" | "ok" | "none";

/** Expired, expiring within 2 days ("soon"), later ("ok"), or no date. */
export function expiryLevel(expiryDate: string | null, today: Date = new Date()): ExpiryLevel {
  if (!expiryDate) return "none";
  const days = daysUntil(expiryDate, today);
  if (days < 0) return "expired";
  if (days <= 2) return "soon";
  return "ok";
}

export function expiryLabel(expiryDate: string | null, today: Date = new Date()): string {
  if (!expiryDate) return "No expiry date";
  const days = daysUntil(expiryDate, today);
  if (days < -1) return `Expired ${-days} days ago`;
  if (days === -1) return "Expired yesterday";
  if (days === 0) return "Expires today";
  if (days === 1) return "Expires tomorrow";
  return `Expires in ${days} days`;
}

/** Quick picks on the add-item screen. */
export const EXPIRY_PRESETS = [
  { label: "Tomorrow", days: 1 },
  { label: "3 days", days: 3 },
  { label: "1 week", days: 7 },
  { label: "2 weeks", days: 14 },
  { label: "1 month", days: 30 },
];
