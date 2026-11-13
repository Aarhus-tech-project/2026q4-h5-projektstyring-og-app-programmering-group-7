import { DarkTheme, DefaultTheme, type Theme } from "expo-router";
import { useColorScheme } from "react-native";

const light = {
  background: "#F5F7FA",
  card: "#FFFFFF",
  text: "#111827",
  muted: "#6B7280",
  border: "#E5E7EB",
  accent: "#208AEF",
  danger: "#DC2626",
  success: "#16A34A",
};

const dark: typeof light = {
  background: "#0B0F14",
  card: "#161B22",
  text: "#F3F4F6",
  muted: "#9CA3AF",
  border: "#30363D",
  accent: "#4BA3F5",
  danger: "#F87171",
  success: "#4ADE80",
};

export type Colors = typeof light;

export function useColors(): Colors {
  return useColorScheme() === "dark" ? dark : light;
}

/**
 * Theme for the navigators (tab bar, headers, screen backgrounds). Without it they
 * stay on the light theme, e.g. inactive tab icons are drawn in the light theme's
 * near-black text color, which is invisible on a dark tab bar.
 */
export function useNavigationTheme(): Theme {
  const isDark = useColorScheme() === "dark";
  const base = isDark ? DarkTheme : DefaultTheme;
  const c = isDark ? dark : light;
  return {
    ...base,
    colors: {
      ...base.colors,
      primary: c.accent,
      background: c.background,
      card: c.card,
      text: c.text,
      border: c.border,
      notification: c.danger,
    },
  };
}
