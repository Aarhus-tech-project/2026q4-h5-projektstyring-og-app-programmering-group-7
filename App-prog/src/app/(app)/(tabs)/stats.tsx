import { useFocusEffect } from "expo-router";
import { useCallback, useState } from "react";
import { ActivityIndicator, RefreshControl, ScrollView, Text, View } from "react-native";

import { useColors, type Colors } from "@/components/theme";
import { BudgetBar, Card, ErrorText } from "@/components/ui";
import { useSession } from "@/context/auth";
import { useCurrentHousehold } from "@/context/household";
import { api, type Stats } from "@/lib/api";
import { formatMoney } from "@/lib/format";

export default function StatsScreen() {
  const c = useColors();
  const { token } = useSession();
  const { household } = useCurrentHousehold();
  const [stats, setStats] = useState<Stats | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(async () => {
    try {
      setStats(await api.households.stats(token!, household.id));
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not load stats.");
    }
  }, [token, household.id]);

  useFocusEffect(
    useCallback(() => {
      load();
    }, [load]),
  );

  if (!stats && !error) {
    return (
      <View style={{ flex: 1, alignItems: "center", justifyContent: "center", backgroundColor: c.background }}>
        <ActivityIndicator color={c.accent} />
      </View>
    );
  }

  const finished = (stats?.used ?? 0) + (stats?.thrownOut ?? 0);
  const wasteRate = finished > 0 ? Math.round(((stats?.thrownOut ?? 0) / finished) * 100) : 0;

  return (
    <ScrollView
      style={{ backgroundColor: c.background }}
      contentContainerStyle={{ padding: 16, gap: 16 }}
      refreshControl={
        <RefreshControl
          refreshing={refreshing}
          onRefresh={async () => {
            setRefreshing(true);
            await load();
            setRefreshing(false);
          }}
        />
      }
    >
      <ErrorText>{error}</ErrorText>
      {stats && (
        <>
          {stats.budget.monthlyBudget !== null ? (
            <Card>
              <BudgetBar budget={stats.budget} />
            </Card>
          ) : (
            <Card>
              <Text style={{ color: c.text, fontWeight: "600" }}>Spent this month</Text>
              <Text style={{ color: c.text, fontSize: 24, fontWeight: "700" }}>{formatMoney(stats.budget.spentThisMonth)}</Text>
              <Text style={{ color: c.muted, fontSize: 13 }}>Set a monthly budget in the Household tab to track it.</Text>
            </Card>
          )}

          <Card>
            <View style={{ flexDirection: "row" }}>
              <Big label="In fridge" value={String(stats.inFridge)} colors={c} />
              <Big label="Used" value={String(stats.used)} colors={c} />
              <Big label="Thrown out" value={String(stats.thrownOut)} colors={c} />
            </View>
          </Card>

          <Card>
            <Text style={{ color: c.text, fontWeight: "600" }}>Food waste</Text>
            <View style={{ flexDirection: "row" }}>
              <Big label="Money wasted" value={formatMoney(stats.moneyWasted)} colors={c} danger />
              <Big label="Of finished items" value={`${wasteRate}%`} colors={c} />
            </View>
          </Card>

          <Card>
            <Text style={{ color: c.text, fontWeight: "600" }}>Thrown out most often</Text>
            {stats.mostThrownOut.length === 0 ? (
              <Text style={{ color: c.muted }}>Nothing thrown out yet. Nice!</Text>
            ) : (
              stats.mostThrownOut.map((w, i) => (
                <View key={w.name} style={{ flexDirection: "row", justifyContent: "space-between" }}>
                  <Text style={{ color: c.text }}>
                    {i + 1}. {w.name}
                  </Text>
                  <Text style={{ color: c.muted }}>{w.count}×</Text>
                </View>
              ))
            )}
          </Card>
        </>
      )}
    </ScrollView>
  );
}

function Big({ label, value, colors: c, danger }: { label: string; value: string; colors: Colors; danger?: boolean }) {
  return (
    <View style={{ flex: 1, alignItems: "center", gap: 2 }}>
      <Text style={{ fontSize: 20, fontWeight: "700", color: danger ? c.danger : c.text }}>{value}</Text>
      <Text style={{ fontSize: 12, color: c.muted }}>{label}</Text>
    </View>
  );
}
