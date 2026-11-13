import { useState } from "react";
import { StyleSheet, Text } from "react-native";

import { KeyboardScroll } from "@/components/KeyboardScroll";

import { useColors } from "@/components/theme";
import { Button, Card, ErrorText, Field } from "@/components/ui";
import { useHousehold } from "@/context/household";
import { parseMoney } from "@/lib/format";

/** Create a new household or join one with an invite code. */
export function HouseholdSetup({ onDone, header }: { onDone?: () => void; header?: React.ReactNode }) {
  const c = useColors();
  const { create, join } = useHousehold();

  const [name, setName] = useState("");
  const [budgetText, setBudgetText] = useState("");
  const [code, setCode] = useState("");
  const [busy, setBusy] = useState<"create" | "join" | null>(null);
  const [error, setError] = useState<string | null>(null);

  const budget = parseMoney(budgetText);
  const budgetInvalid = budgetText.trim() !== "" && budget === null;

  async function run(kind: "create" | "join", action: () => Promise<void>) {
    setError(null);
    setBusy(kind);
    try {
      await action();
      onDone?.();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Something went wrong.");
    } finally {
      setBusy(null);
    }
  }

  return (
    <KeyboardScroll
        style={{ backgroundColor: c.background }}
        contentContainerStyle={styles.container}
      >
        {header}
        <ErrorText>{error}</ErrorText>

        <Card>
          <Text style={[styles.title, { color: c.text }]}>Join your roommates</Text>
          <Text style={{ color: c.muted }}>Ask someone in the household for the invite code.</Text>
          <Field
            placeholder="Invite code, e.g. K7M2QX9P"
            value={code}
            onChangeText={setCode}
            autoCapitalize="characters"
            autoCorrect={false}
          />
          <Button
            label="Join household"
            onPress={() => run("join", () => join(code))}
            disabled={!code.trim()}
            loading={busy === "join"}
          />
        </Card>

        <Text style={[styles.or, { color: c.muted }]}>or</Text>

        <Card>
          <Text style={[styles.title, { color: c.text }]}>Create a household</Text>
          <Field label="Name" placeholder="e.g. Flat 4B" value={name} onChangeText={setName} />
          <Field
            label="Monthly food budget (optional)"
            placeholder="e.g. 3000"
            value={budgetText}
            onChangeText={setBudgetText}
            keyboardType="decimal-pad"
          />
          {budgetInvalid && <ErrorText>Enter an amount like 3000 or 2500,50</ErrorText>}
          <Text style={{ color: c.muted, fontSize: 13 }}>
            With a budget, FridgeShare warns you when a purchase would go over it.
          </Text>
          <Button
            label="Create household"
            onPress={() => run("create", () => create(name.trim(), budget))}
            disabled={!name.trim() || budgetInvalid}
            loading={busy === "create"}
          />
        </Card>
    </KeyboardScroll>
  );
}

const styles = StyleSheet.create({
  container: { padding: 16, gap: 16, flexGrow: 1, justifyContent: "center", maxWidth: 480, width: "100%", alignSelf: "center" },
  title: { fontSize: 18, fontWeight: "700" },
  or: { textAlign: "center" },
});
