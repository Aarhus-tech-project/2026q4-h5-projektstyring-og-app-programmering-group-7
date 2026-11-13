import { createContext, use, useCallback, useEffect, useState, type PropsWithChildren } from "react";

import { useSession } from "@/context/auth";
import { api, ApiError, type BudgetStatus, type Household, type HouseholdSummary } from "@/lib/api";
import { getItem, setItem } from "@/lib/storage";

// Remembered on the device so the app reopens in the household you used last.
const LAST_HOUSEHOLD_KEY = "last-household-id";

type HouseholdContextValue = {
  /** All households the user is in. */
  households: HouseholdSummary[];
  /** The one currently shown (null = user has none yet → onboarding). */
  household: Household | null;
  budget: BudgetStatus | null;
  isLoading: boolean;
  refresh: () => Promise<void>;
  refreshBudget: () => Promise<void>;
  switchTo: (id: number) => Promise<void>;
  create: (name: string, monthlyBudget: number | null) => Promise<void>;
  join: (inviteCode: string) => Promise<void>;
  leave: () => Promise<void>;
  /** Replace the current household after an update (rename, budget, new invite code). */
  setHousehold: (h: Household) => void;
};

const HouseholdContext = createContext<HouseholdContextValue | null>(null);

export function useHousehold() {
  const value = use(HouseholdContext);
  if (!value) throw new Error("useHousehold must be used inside <HouseholdProvider />");
  return value;
}

/** For screens that only render inside a household. */
export function useCurrentHousehold() {
  const ctx = useHousehold();
  if (!ctx.household) throw new Error("No household selected");
  return { ...ctx, household: ctx.household };
}

/** Fresh household state for every login/logout (keyed by the session token). */
export function HouseholdProvider({ children }: PropsWithChildren) {
  const { token } = useSession();
  return (
    <HouseholdState key={token ?? "logged-out"} token={token}>
      {children}
    </HouseholdState>
  );
}

function HouseholdState({ children, token }: PropsWithChildren<{ token: string | null }>) {
  const [households, setHouseholds] = useState<HouseholdSummary[]>([]);
  const [household, setHousehold] = useState<Household | null>(null);
  const [budget, setBudget] = useState<BudgetStatus | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const select = useCallback(
    async (id: number | null) => {
      if (!token || id === null) {
        setHousehold(null);
        setBudget(null);
        await setItem(LAST_HOUSEHOLD_KEY, null);
        return;
      }
      // Independent requests: run them in parallel
      const [detail, status] = await Promise.all([api.households.get(token, id), api.households.budget(token, id)]);
      setHousehold(detail);
      setBudget(status);
      await setItem(LAST_HOUSEHOLD_KEY, String(id));
    },
    [token],
  );

  const refresh = useCallback(async () => {
    if (!token) return;
    const list = await api.households.list(token);
    setHouseholds(list);
    const last = Number(await getItem(LAST_HOUSEHOLD_KEY));
    const pick = list.find((h) => h.id === last) ?? list[0] ?? null;
    await select(pick?.id ?? null);
  }, [token, select]);

  // Load once per login. (The provider is re-mounted when the user changes, see
  // HouseholdProvider below, so there's no stale state to clear on logout.)
  useEffect(() => {
    if (!token) return;
    let cancelled = false;
    (async () => {
      try {
        const list = await api.households.list(token);
        if (cancelled) return;
        setHouseholds(list);
        const last = Number(await getItem(LAST_HOUSEHOLD_KEY));
        const pick = list.find((h) => h.id === last) ?? list[0] ?? null;
        if (!cancelled) await select(pick?.id ?? null);
      } catch (e) {
        if (!(e instanceof ApiError && e.status === 401)) console.warn("Could not load households", e);
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [token, select]);

  const refreshBudget = useCallback(async () => {
    if (token && household) setBudget(await api.households.budget(token, household.id));
  }, [token, household]);

  const value: HouseholdContextValue = {
    households,
    household,
    budget,
    isLoading: !!token && isLoading,
    refresh,
    refreshBudget,
    switchTo: (id) => select(id),
    create: async (name, monthlyBudget) => {
      const created = await api.households.create(token!, name, monthlyBudget);
      await setItem(LAST_HOUSEHOLD_KEY, String(created.id));
      await refresh();
    },
    join: async (code) => {
      const joined = await api.households.join(token!, code);
      await setItem(LAST_HOUSEHOLD_KEY, String(joined.id));
      await refresh();
    },
    leave: async () => {
      if (!household) return;
      await api.households.leave(token!, household.id);
      await setItem(LAST_HOUSEHOLD_KEY, null);
      await refresh();
    },
    setHousehold: (h) => {
      setHousehold(h);
      refreshBudget();
    },
  };

  return <HouseholdContext value={value}>{children}</HouseholdContext>;
}
