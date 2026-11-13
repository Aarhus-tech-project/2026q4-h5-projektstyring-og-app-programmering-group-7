import { Stack } from "expo-router";

/** Tabs, plus screens that open on top of them as modals. */
export default function AppLayout() {
  return (
    <Stack>
      <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
      <Stack.Screen name="add-item" options={{ presentation: "modal", title: "Add item" }} />
      <Stack.Screen name="new-household" options={{ presentation: "modal", title: "Add household" }} />
    </Stack>
  );
}
