import { createContext, use, useEffect, useState, type PropsWithChildren } from "react";

import { api, ApiError, type User } from "@/lib/api";
import { getItem, setItem } from "@/lib/storage";

const TOKEN_KEY = "auth-token";

type AuthContextValue = {
  token: string | null;
  user: User | null;
  isLoading: boolean;
  signIn: (email: string, password: string) => Promise<void>;
  signUp: (email: string, username: string, password: string) => Promise<void>;
  signOut: () => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function useSession() {
  const value = use(AuthContext);
  if (!value) throw new Error("useSession must be used inside <SessionProvider />");
  return value;
}

export function SessionProvider({ children }: PropsWithChildren) {
  const [token, setToken] = useState<string | null>(null);
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  // Restore a saved session on startup and check it's still valid.
  useEffect(() => {
    (async () => {
      try {
        const saved = await getItem(TOKEN_KEY);
        if (!saved) return;
        try {
          setUser(await api.me(saved));
          setToken(saved);
        } catch (e) {
          // Expired/invalid token: forget it. If the server is just unreachable, keep it.
          if (e instanceof ApiError && e.status === 401) await setItem(TOKEN_KEY, null);
          else setToken(saved);
        }
      } finally {
        setIsLoading(false);
      }
    })();
  }, []);

  async function saveSession(newToken: string, newUser: User) {
    await setItem(TOKEN_KEY, newToken);
    setUser(newUser);
    setToken(newToken);
  }

  const value: AuthContextValue = {
    token,
    user,
    isLoading,
    signIn: async (email, password) => {
      const res = await api.login(email, password);
      await saveSession(res.token, res.user);
    },
    signUp: async (email, username, password) => {
      const res = await api.register(email, username, password);
      await saveSession(res.token, res.user);
    },
    signOut: async () => {
      await setItem(TOKEN_KEY, null);
      setUser(null);
      setToken(null);
    },
  };

  return <AuthContext value={value}>{children}</AuthContext>;
}
