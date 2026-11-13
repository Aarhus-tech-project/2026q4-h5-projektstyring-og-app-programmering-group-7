import Constants from "expo-constants";
import { Platform } from "react-native";

const API_PORT = 5258;

/**
 * Base URL of the ASP.NET API.
 * - Set EXPO_PUBLIC_API_URL (in App-prog/.env.local) to override. Required with `expo start --tunnel`.
 * - On web, the API runs on the same machine: localhost.
 * - On a phone in LAN mode, reuse the IP of the computer running the Expo dev server.
 * Returns null when it can't be worked out (tunnel mode without EXPO_PUBLIC_API_URL).
 */
function resolveBaseUrl(): string | null {
  const fromEnv = process.env.EXPO_PUBLIC_API_URL;
  if (fromEnv) return fromEnv.replace(/\/$/, "");

  if (Platform.OS === "web") return `http://localhost:${API_PORT}`;

  const devHost = Constants.expoConfig?.hostUri?.split(":")[0];
  if (devHost) {
    // LAN mode gives an IP address. In tunnel mode it's the tunnel's hostname
    // (e.g. xyz.on.expo.app), which only forwards to Expo, not to the API.
    const isLocal = /^\d{1,3}(\.\d{1,3}){3}$/.test(devHost) || devHost === "localhost";
    return isLocal ? `http://${devHost}:${API_PORT}` : null;
  }

  // Android emulator's alias for the host machine
  return Platform.OS === "android" ? `http://10.0.2.2:${API_PORT}` : `http://localhost:${API_PORT}`;
}

export const API_URL = resolveBaseUrl();

export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
    /** Parsed JSON error body, e.g. the over-budget details. */
    public body: unknown = null,
  ) {
    super(message);
  }
}

async function request<T>(path: string, init: RequestInit = {}, token?: string | null): Promise<T> {
  if (!API_URL) {
    throw new ApiError(
      "Expo is running in tunnel mode, so the app can't find the API. Set EXPO_PUBLIC_API_URL in App-prog/.env.local to a public URL of the API and restart Expo.",
      0,
    );
  }

  let res: Response;
  try {
    res = await fetch(`${API_URL}${path}`, {
      ...init,
      headers: {
        "Content-Type": "application/json",
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...init.headers,
      },
    });
  } catch {
    throw new ApiError(`Could not reach the server at ${API_URL}.`, 0);
  }

  const body = res.status === 204 ? null : await res.json().catch(() => null);
  if (!res.ok) throw new ApiError(errorMessage(body) ?? `Request failed (${res.status}).`, res.status, body);
  return body as T;
}

// Handles both `{ message }` and ASP.NET validation problem details `{ errors: { Field: [..] } }`.
function errorMessage(body: any): string | null {
  if (!body) return null;
  if (typeof body.message === "string") return body.message;
  if (body.errors) {
    const first = Object.values(body.errors).flat()[0];
    if (typeof first === "string") return first;
  }
  return typeof body.title === "string" ? body.title : null;
}

const json = (method: string, data?: unknown): RequestInit => ({
  method,
  body: data === undefined ? undefined : JSON.stringify(data),
});

// ---------- Types (mirror the API's DTOs) ----------

export type User = { id: number; email: string; username: string };
export type AuthResponse = { token: string; expiresAt: string; user: User };

export type Role = "Owner" | "Member";
export type HouseholdSummary = { id: number; name: string; role: Role; memberCount: number };
export type Member = { userId: number; username: string; role: Role; joinedAt: string };
export type Household = {
  id: number;
  name: string;
  inviteCode: string;
  inviteExpiresAt: string;
  monthlyBudget: number | null;
  myRole: Role;
  members: Member[];
};

export type BudgetStatus = {
  monthlyBudget: number | null;
  spentThisMonth: number;
  remaining: number | null;
  percentUsed: number | null;
  overBudget: boolean;
  monthStart: string;
};
export type OverBudget = { code: "over_budget"; message: string; current: BudgetStatus; afterAdding: BudgetStatus };

export type ItemStatus = "InFridge" | "Used" | "ThrownOut";
export type Item = {
  id: number;
  name: string;
  barcode: string | null;
  imageUrl: string | null;
  quantity: number;
  /** "YYYY-MM-DD" */
  expiryDate: string | null;
  price: number | null;
  status: ItemStatus;
  addedBy: string;
  createdAt: string;
};
export type NewItem = {
  name: string;
  barcode?: string | null;
  imageUrl?: string | null;
  quantity: number;
  expiryDate?: string | null;
  price?: number | null;
  allowOverBudget?: boolean;
};
export type CreateItemResponse = { item: Item; budget: BudgetStatus };
export type StatusChange = { item: Item; addedToShoppingList: boolean };

export type ShoppingItem = { id: number; name: string; checked: boolean; createdAt: string };
export type Product = { barcode: string; name: string; brand: string | null; imageUrl: string | null };
export type Stats = {
  inFridge: number;
  used: number;
  thrownOut: number;
  moneyWasted: number;
  mostThrownOut: { name: string; count: number }[];
  budget: BudgetStatus;
};

export function isOverBudget(e: unknown): e is ApiError & { body: OverBudget } {
  return e instanceof ApiError && e.status === 409 && (e.body as OverBudget | null)?.code === "over_budget";
}

// ---------- Endpoints ----------

export const api = {
  login: (email: string, password: string) =>
    request<AuthResponse>("/api/auth/login", json("POST", { email, password })),
  register: (email: string, username: string, password: string) =>
    request<AuthResponse>("/api/auth/register", json("POST", { email, username, password })),
  me: (token: string) => request<User>("/api/auth/me", {}, token),

  households: {
    list: (token: string) => request<HouseholdSummary[]>("/api/households", {}, token),
    get: (token: string, id: number) => request<Household>(`/api/households/${id}`, {}, token),
    create: (token: string, name: string, monthlyBudget: number | null) =>
      request<Household>("/api/households", json("POST", { name, monthlyBudget }), token),
    join: (token: string, inviteCode: string) =>
      request<Household>("/api/households/join", json("POST", { inviteCode }), token),
    update: (token: string, id: number, name: string, monthlyBudget: number | null) =>
      request<Household>(`/api/households/${id}`, json("PUT", { name, monthlyBudget }), token),
    newInviteCode: (token: string, id: number) =>
      request<Household>(`/api/households/${id}/invite-code`, json("POST"), token),
    leave: (token: string, id: number) => request<null>(`/api/households/${id}/members/me`, json("DELETE"), token),
    budget: (token: string, id: number) => request<BudgetStatus>(`/api/households/${id}/budget`, {}, token),
    stats: (token: string, id: number) => request<Stats>(`/api/households/${id}/stats`, {}, token),
  },

  items: {
    list: (token: string, householdId: number, skip = 0, take = 50) =>
      request<Item[]>(`/api/households/${householdId}/items?skip=${skip}&take=${take}`, {}, token),
    create: (token: string, householdId: number, item: NewItem) =>
      request<CreateItemResponse>(`/api/households/${householdId}/items`, json("POST", item), token),
    useOne: (token: string, householdId: number, id: number) =>
      request<StatusChange>(`/api/households/${householdId}/items/${id}/use`, json("POST"), token),
    throwOut: (token: string, householdId: number, id: number) =>
      request<StatusChange>(`/api/households/${householdId}/items/${id}/throw-out`, json("POST"), token),
    remove: (token: string, householdId: number, id: number) =>
      request<null>(`/api/households/${householdId}/items/${id}`, json("DELETE"), token),
  },

  shopping: {
    list: (token: string, householdId: number) =>
      request<ShoppingItem[]>(`/api/households/${householdId}/shopping`, {}, token),
    add: (token: string, householdId: number, name: string) =>
      request<ShoppingItem>(`/api/households/${householdId}/shopping`, json("POST", { name }), token),
    setChecked: (token: string, householdId: number, id: number, checked: boolean) =>
      request<ShoppingItem>(`/api/households/${householdId}/shopping/${id}`, json("PATCH", { checked }), token),
    remove: (token: string, householdId: number, id: number) =>
      request<null>(`/api/households/${householdId}/shopping/${id}`, json("DELETE"), token),
    clearChecked: (token: string, householdId: number) =>
      request<null>(`/api/households/${householdId}/shopping/checked`, json("DELETE"), token),
  },

  products: {
    lookup: (token: string, barcode: string) => request<Product>(`/api/products/${barcode}`, {}, token),
  },
};
