import type { PropsWithChildren } from "react";
import {
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  View,
  type TextInputProps,
  type ViewStyle,
} from "react-native";

import { useColors } from "@/components/theme";
import type { BudgetStatus } from "@/lib/api";
import { budgetProgress } from "@/lib/budget";
import { formatMoney } from "@/lib/format";

type ButtonProps = {
  label: string;
  onPress: () => void;
  variant?: "primary" | "secondary" | "danger";
  disabled?: boolean;
  loading?: boolean;
  style?: ViewStyle;
};

export function Button({ label, onPress, variant = "primary", disabled, loading, style }: ButtonProps) {
  const c = useColors();
  const bg = variant === "primary" ? c.accent : variant === "danger" ? c.danger : c.card;
  const fg = variant === "secondary" ? c.text : "#fff";
  return (
    <Pressable
      onPress={onPress}
      disabled={disabled || loading}
      style={({ pressed }) => [
        styles.button,
        { backgroundColor: bg, borderColor: c.border, borderWidth: variant === "secondary" ? 1 : 0 },
        (disabled || pressed) && { opacity: 0.6 },
        style,
      ]}
    >
      {loading ? <ActivityIndicator color={fg} /> : <Text style={[styles.buttonText, { color: fg }]}>{label}</Text>}
    </Pressable>
  );
}

export function Card({ children, style }: PropsWithChildren<{ style?: ViewStyle }>) {
  const c = useColors();
  return <View style={[styles.card, { backgroundColor: c.card }, style]}>{children}</View>;
}

export function Field({ label, ...props }: TextInputProps & { label?: string }) {
  const c = useColors();
  return (
    <View style={{ gap: 6 }}>
      {label && <Text style={{ color: c.muted, fontSize: 13, fontWeight: "600" }}>{label}</Text>}
      <TextInput
        placeholderTextColor={c.muted}
        {...props}
        style={[styles.input, { backgroundColor: c.background, color: c.text, borderColor: c.border }, props.style]}
      />
    </View>
  );
}

export function ErrorText({ children }: PropsWithChildren) {
  const c = useColors();
  if (!children) return null;
  return <Text style={{ color: c.danger }}>{children}</Text>;
}

/** "Spent 1.250,00 kr of 3.000,00 kr" with a progress bar that turns red over budget. */
export function BudgetBar({ budget }: { budget: BudgetStatus }) {
  const c = useColors();
  if (budget.monthlyBudget === null) return null;
  const progress = budgetProgress(budget);
  const color = budget.overBudget ? c.danger : progress >= 0.9 ? "#F59E0B" : c.success;
  return (
    <View style={{ gap: 6 }}>
      <View style={{ flexDirection: "row", justifyContent: "space-between" }}>
        <Text style={{ color: c.text, fontWeight: "600" }}>Food budget this month</Text>
        <Text style={{ color: budget.overBudget ? c.danger : c.muted }}>
          {budget.overBudget
            ? `${formatMoney(-(budget.remaining ?? 0))} over`
            : `${formatMoney(budget.remaining ?? 0)} left`}
        </Text>
      </View>
      <View style={[styles.track, { backgroundColor: c.border }]}>
        <View style={[styles.fill, { width: `${progress * 100}%`, backgroundColor: color }]} />
      </View>
      <Text style={{ color: c.muted, fontSize: 12 }}>
        Spent {formatMoney(budget.spentThisMonth)} of {formatMoney(budget.monthlyBudget)}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  button: { height: 48, borderRadius: 10, alignItems: "center", justifyContent: "center", paddingHorizontal: 16 },
  buttonText: { fontSize: 16, fontWeight: "600" },
  card: { borderRadius: 12, padding: 16, gap: 12 },
  input: { borderWidth: 1, borderRadius: 10, paddingHorizontal: 14, paddingVertical: 12, fontSize: 16 },
  track: { height: 8, borderRadius: 4, overflow: "hidden" },
  fill: { height: 8, borderRadius: 4 },
});
