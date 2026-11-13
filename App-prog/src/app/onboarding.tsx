import { Pressable, Text, View } from "react-native";
import { SafeAreaView } from "react-native-safe-area-context";

import { HouseholdSetup } from "@/components/HouseholdSetup";
import { useColors } from "@/components/theme";
import { useSession } from "@/context/auth";

/** Shown after login when the user isn't in any household yet. */
export default function Onboarding() {
  const c = useColors();
  const { user, signOut } = useSession();

  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: c.background }}>
      <HouseholdSetup
        header={
          <View style={{ gap: 4 }}>
            <Text style={{ fontSize: 28, fontWeight: "800", color: c.text }}>
              Welcome{user ? `, ${user.username}` : ""}!
            </Text>
            <Text style={{ color: c.muted, fontSize: 16 }}>
              FridgeShare keeps track of your shared fridge. Start by joining or creating a household.
            </Text>
          </View>
        }
      />
      <Pressable onPress={signOut} style={{ padding: 16, alignItems: "center" }}>
        <Text style={{ color: c.muted }}>Log out</Text>
      </Pressable>
    </SafeAreaView>
  );
}
