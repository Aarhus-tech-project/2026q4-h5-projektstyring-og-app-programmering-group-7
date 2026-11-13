/** 1234.5 → "1.234,50 kr" (Danish style). */
export function formatMoney(amount: number): string {
  const negative = amount < 0;
  const [whole, decimals] = Math.abs(amount).toFixed(2).split(".");
  const grouped = whole.replace(/\B(?=(\d{3})+(?!\d))/g, ".");
  return `${negative ? "-" : ""}${grouped},${decimals} kr`;
}

/** Parses "12,50", "12.5" or "1.234,50" into a number. Returns null for empty/invalid input. */
export function parseMoney(input: string): number | null {
  const s = input.trim().replace(/\s|kr/gi, "");
  if (!s) return null;
  // "1.234,50" → thousands dot + decimal comma; "12.50" → decimal dot
  const normalized = s.includes(",") ? s.replace(/\./g, "").replace(",", ".") : s;
  if (!/^\d+(\.\d{1,2})?$/.test(normalized)) return null;
  return Number(normalized);
}

/** Timestamp from the API → "dd-mm-yyyy" in local time. */
export function formatDate(isoDateTime: string): string {
  const date = new Date(isoDateTime);
  const d = String(date.getDate()).padStart(2, "0");
  const m = String(date.getMonth() + 1).padStart(2, "0");
  return `${d}-${m}-${date.getFullYear()}`;
}
