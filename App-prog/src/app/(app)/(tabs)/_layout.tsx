import Ionicons from "@expo/vector-icons/Ionicons";
import { Tabs } from "expo-router";

import { useColors } from "@/components/theme";
import { useCurrentHousehold } from "@/context/household";

export default function TabsLayout() {
  const colors = useColors();
  const { household } = useCurrentHousehold();

  return (
    <Tabs
      screenOptions={{
        tabBarActiveTintColor: colors.accent,
        tabBarInactiveTintColor: colors.muted,
        // Don't push the tab bar up on top of the keyboard
        tabBarHideOnKeyboard: true,
        tabBarStyle: { backgroundColor: colors.card, borderTopColor: colors.border },
        headerStyle: { backgroundColor: colors.card },
        headerTintColor: colors.text,
      }}
    >
      <Tabs.Screen
        name="index"
        options={{
          title: household.name,
          tabBarLabel: "Fridge",
          tabBarIcon: ({ color, size }) => <Ionicons name="snow-outline" size={size} color={color as string} />,
        }}
      />
      <Tabs.Screen
        name="shopping"
        options={{
          title: "Shopping list",
          tabBarLabel: "Shopping",
          tabBarIcon: ({ color, size }) => <Ionicons name="cart-outline" size={size} color={color as string} />,
        }}
      />
      <Tabs.Screen
        name="stats"
        options={{
          title: "Stats",
          tabBarIcon: ({ color, size }) => <Ionicons name="stats-chart-outline" size={size} color={color as string} />,
        }}
      />
      <Tabs.Screen
        name="settings"
        options={{
          title: "Household",
          tabBarIcon: ({ color, size }) => <Ionicons name="home-outline" size={size} color={color as string} />,
        }}
      />
    </Tabs>
  );
}
