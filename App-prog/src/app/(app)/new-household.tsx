import { router } from "expo-router";

import { HouseholdSetup } from "@/components/HouseholdSetup";

/** Join or create another household (from the Household tab). */
export default function NewHousehold() {
  return <HouseholdSetup onDone={() => router.back()} />;
}
