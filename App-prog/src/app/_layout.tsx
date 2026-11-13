import { SplashScreen, Stack, ThemeProvider } from "expo-router";
import { StatusBar } from "expo-status-bar";

import { useNavigationTheme } from "@/components/theme";
import { SessionProvider, useSession } from "@/context/auth";
import { HouseholdProvider, useHousehold } from "@/context/household";

SplashScreen.preventAutoHideAsync();

export default function RootLayout() {
  return (
    <ThemeProvider value={useNavigationTheme()}>
      <StatusBar style="auto" />
      <SessionProvider>
        <HouseholdProvider>
          <SplashScreenController />
          <RootNavigator />
        </HouseholdProvider>
      </SessionProvider>
    </ThemeProvider>
  );
}

function SplashScreenController() {
  const { isLoading } = useSession();
  const { isLoading: householdsLoading } = useHousehold();
  if (!isLoading && !householdsLoading) SplashScreen.hide();
  return null;
}

/**
 * Three states:
 * - logged out                 → login
 * - logged in, no household    → onboarding (create or join one)
 * - logged in, in a household  → the app
 */
function RootNavigator() {
  const { token, isLoading } = useSession();
  const { household, isLoading: householdsLoading } = useHousehold();
  if (isLoading || householdsLoading) return null;

  const loggedIn = !!token;
  return (
    <Stack screenOptions={{ headerShown: false }}>
      <Stack.Protected guard={loggedIn && !!household}>
        <Stack.Screen name="(app)" />
      </Stack.Protected>
      <Stack.Protected guard={loggedIn && !household}>
        <Stack.Screen name="onboarding" />
      </Stack.Protected>
      <Stack.Protected guard={!loggedIn}>
        <Stack.Screen name="login" />
      </Stack.Protected>
    </Stack>
  );
}
