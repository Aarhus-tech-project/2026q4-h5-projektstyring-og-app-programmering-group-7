import { router } from "expo-router";
import { useState } from "react";
import { Pressable, Share, Text, View } from "react-native";

import { KeyboardScroll } from "@/components/KeyboardScroll";

import { useColors } from "@/components/theme";
import { Button, Card, ErrorText, Field } from "@/components/ui";
import { useSession } from "@/context/auth";
import { useCurrentHousehold } from "@/context/household";
import { api } from "@/lib/api";
import { confirm } from "@/lib/confirm";
import { formatDate, formatMoney, parseMoney } from "@/lib/format";

export default function HouseholdSettings() {
  const c = useColors();
  const { token, user, signOut } = useSession();
  const { household, households, switchTo, setHousehold, leave } = useCurrentHousehold();
  const isOwner = household.myRole === "Owner";

  const [name, setName] = useState(household.name);
  const [budgetText, setBudgetText] = useState(household.monthlyBudget?.toString().replace(".", ",") ?? "");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  const budget = parseMoney(budgetText);
  const budgetInvalid = budgetText.trim() !== "" && budget === null;
  const changed = name.trim() !== household.name || budget !== household.monthlyBudget;

  async function guarded(action: () => Promise<void>) {
    setError(null);
    try {
      await action();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Something went wrong.");
    }
  }

  async function saveSettings() {
    setSaving(true);
    setSaved(false);
    await guarded(async () => {
      setHousehold(await api.households.update(token!, household.id, name.trim(), budget));
      setSaved(true);
    });
    setSaving(false);
  }

  async function shareInvite() {
    await Share.share({
      message: `Join "${household.name}" on FridgeShare with invite code ${household.inviteCode} (valid until ${formatDate(household.inviteExpiresAt)}).`,
    });
  }

  async function confirmLeave() {
    const ok = await confirm(
      "Leave household",
      household.members.length === 1
        ? "You're the last member, so the household and all its items will be deleted."
        : "You'll lose access to this household's fridge and shopping list.",
      "Leave",
      true,
    );
    if (ok) await guarded(leave);
  }

  return (
    <KeyboardScroll style={{ backgroundColor: c.background }} contentContainerStyle={{ padding: 16, gap: 16 }}>
      <ErrorText>{error}</ErrorText>

      <Card>
        <Text style={{ color: c.text, fontWeight: "700", fontSize: 16 }}>Invite roommates</Text>
        <Text selectable style={{ color: c.text, fontSize: 28, fontWeight: "800", letterSpacing: 4 }}>
          {household.inviteCode}
        </Text>
        <Text style={{ color: c.muted, fontSize: 13 }}>Valid until {formatDate(household.inviteExpiresAt)}</Text>
        <View style={{ flexDirection: "row", gap: 8 }}>
          <Button label="Share code" onPress={shareInvite} style={{ flex: 1 }} />
          {isOwner && (
            <Button
              label="New code"
              variant="secondary"
              style={{ flex: 1 }}
              onPress={() => guarded(async () => setHousehold(await api.households.newInviteCode(token!, household.id)))}
            />
          )}
        </View>
      </Card>

      <Card>
        <Text style={{ color: c.text, fontWeight: "700", fontSize: 16 }}>Household</Text>
        {isOwner ? (
          <>
            <Field label="Name" value={name} onChangeText={setName} />
            <Field
              label="Monthly food budget"
              placeholder="No budget"
              value={budgetText}
              onChangeText={setBudgetText}
              keyboardType="decimal-pad"
            />
            {budgetInvalid && <ErrorText>Enter an amount like 3000 or 2500,50</ErrorText>}
            <Text style={{ color: c.muted, fontSize: 13 }}>
              New items with a price are checked against this. Leave empty for no budget.
            </Text>
            <Button label={saved && !changed ? "Saved" : "Save"} onPress={saveSettings} disabled={!changed || !name.trim() || budgetInvalid} loading={saving} />
          </>
        ) : (
          <>
            <Text style={{ color: c.text }}>{household.name}</Text>
            <Text style={{ color: c.muted }}>
              Monthly budget: {household.monthlyBudget !== null ? formatMoney(household.monthlyBudget) : "none"}
            </Text>
            <Text style={{ color: c.muted, fontSize: 13 }}>Only the owner can change these.</Text>
          </>
        )}
      </Card>

      <Card>
        <Text style={{ color: c.text, fontWeight: "700", fontSize: 16 }}>Members ({household.members.length})</Text>
        {household.members.map((m) => (
          <View key={m.userId} style={{ flexDirection: "row", justifyContent: "space-between" }}>
            <Text style={{ color: c.text }}>
              {m.username}
              {m.userId === user?.id ? " (you)" : ""}
            </Text>
            <Text style={{ color: c.muted }}>{m.role}</Text>
          </View>
        ))}
      </Card>

      {households.length > 1 && (
        <Card>
          <Text style={{ color: c.text, fontWeight: "700", fontSize: 16 }}>Switch household</Text>
          {households.map((h) => (
            <Pressable key={h.id} onPress={() => guarded(() => switchTo(h.id))} disabled={h.id === household.id}>
              <Text style={{ color: h.id === household.id ? c.accent : c.text, paddingVertical: 6 }}>
                {h.id === household.id ? "✓ " : ""}
                {h.name} · {h.memberCount} member{h.memberCount === 1 ? "" : "s"}
              </Text>
            </Pressable>
          ))}
        </Card>
      )}

      <Button label="Join or create another household" variant="secondary" onPress={() => router.push("/new-household")} />
      <Button label="Leave household" variant="secondary" onPress={confirmLeave} />
      <Button label={`Log out${user ? ` (${user.username})` : ""}`} variant="danger" onPress={signOut} />
    </KeyboardScroll>
  );
}
