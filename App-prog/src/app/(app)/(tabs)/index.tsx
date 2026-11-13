import Ionicons from "@expo/vector-icons/Ionicons";
import { Image } from "expo-image";
import { router, useFocusEffect } from "expo-router";
import { useCallback, useState } from "react";
import { ActivityIndicator, FlatList, Pressable, RefreshControl, StyleSheet, Text, View } from "react-native";

import { useColors, type Colors } from "@/components/theme";
import { BudgetBar, Card, ErrorText } from "@/components/ui";
import { useSession } from "@/context/auth";
import { useCurrentHousehold } from "@/context/household";
import { api, type Item } from "@/lib/api";
import { expiryLabel, expiryLevel, formatDisplayDate, type ExpiryLevel } from "@/lib/expiry";
import { formatMoney } from "@/lib/format";

const PAGE_SIZE = 50;

export default function FridgeScreen() {
  const c = useColors();
  const styles = makeStyles(c);
  const { token } = useSession();
  const { household, budget, refreshBudget } = useCurrentHousehold();

  const [items, setItems] = useState<Item[] | null>(null);
  const [hasMore, setHasMore] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [openId, setOpenId] = useState<number | null>(null);

  const load = useCallback(async () => {
    try {
      const [page] = await Promise.all([api.items.list(token!, household.id, 0, PAGE_SIZE), refreshBudget()]);
      setItems(page);
      setHasMore(page.length === PAGE_SIZE);
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not load the fridge.");
    }
  }, [token, household.id, refreshBudget]);

  // Reload whenever the tab is shown (e.g. after adding an item)
  useFocusEffect(
    useCallback(() => {
      load();
    }, [load]),
  );

  // Paging: only fetch the next 50 when the user scrolls near the end
  async function loadMore() {
    if (!hasMore || loadingMore || !items) return;
    setLoadingMore(true);
    try {
      const next = await api.items.list(token!, household.id, items.length, PAGE_SIZE);
      setItems([...items, ...next]);
      setHasMore(next.length === PAGE_SIZE);
    } finally {
      setLoadingMore(false);
    }
  }

  async function refresh() {
    setRefreshing(true);
    await load();
    setRefreshing(false);
  }

  async function act(item: Item, action: "use" | "throw" | "delete") {
    setOpenId(null);
    try {
      if (action === "delete") {
        await api.items.remove(token!, household.id, item.id);
        setNotice(`Deleted ${item.name}.`);
      } else {
        const result =
          action === "use"
            ? await api.items.useOne(token!, household.id, item.id)
            : await api.items.throwOut(token!, household.id, item.id);
        setNotice(
          result.addedToShoppingList
            ? `That was the last ${item.name}. Added it to the shopping list.`
            : action === "throw"
              ? `Threw out ${item.name}.`
              : null,
        );
      }
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Something went wrong.");
    }
  }

  if (items === null && !error) {
    return (
      <View style={[styles.center, { backgroundColor: c.background }]}>
        <ActivityIndicator color={c.accent} />
      </View>
    );
  }

  return (
    <View style={{ flex: 1, backgroundColor: c.background }}>
      <FlatList
        data={items ?? []}
        keyExtractor={(i) => String(i.id)}
        contentContainerStyle={styles.list}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={refresh} />}
        onEndReached={loadMore}
        onEndReachedThreshold={0.5}
        ListHeaderComponent={
          <View style={{ gap: 12 }}>
            {budget && budget.monthlyBudget !== null && (
              <Card>
                <BudgetBar budget={budget} />
              </Card>
            )}
            <ErrorText>{error}</ErrorText>
            {notice && (
              <Pressable onPress={() => setNotice(null)}>
                <Text style={styles.notice}>{notice}</Text>
              </Pressable>
            )}
          </View>
        }
        ListEmptyComponent={
          !error ? (
            <Text style={styles.empty}>The fridge is empty. Tap + to add what you bought.</Text>
          ) : undefined
        }
        ListFooterComponent={loadingMore ? <ActivityIndicator color={c.accent} /> : undefined}
        renderItem={({ item }) => (
          <ItemRow
            item={item}
            open={openId === item.id}
            onPress={() => setOpenId(openId === item.id ? null : item.id)}
            onAction={(a) => act(item, a)}
            colors={c}
          />
        )}
      />

      <Pressable
        accessibilityLabel="Add item"
        onPress={() => router.push("/add-item")}
        style={({ pressed }) => [styles.fab, pressed && { opacity: 0.8 }]}
      >
        <Ionicons name="add" size={32} color="#fff" />
      </Pressable>
    </View>
  );
}

function ItemRow({
  item,
  open,
  onPress,
  onAction,
  colors: c,
}: {
  item: Item;
  open: boolean;
  onPress: () => void;
  onAction: (a: "use" | "throw" | "delete") => void;
  colors: Colors;
}) {
  const level = expiryLevel(item.expiryDate);
  const badge = badgeColors(level, c);
  return (
    <Pressable onPress={onPress} style={{ backgroundColor: c.card, borderRadius: 12, padding: 12, gap: 10 }}>
      <View style={{ flexDirection: "row", gap: 12, alignItems: "center" }}>
        {item.imageUrl ? (
          // expo-image caches on disk and only loads images that are on screen
          <Image source={item.imageUrl} style={{ width: 48, height: 48, borderRadius: 8 }} contentFit="cover" transition={150} />
        ) : (
          <View style={{ width: 48, height: 48, borderRadius: 8, backgroundColor: c.border, alignItems: "center", justifyContent: "center" }}>
            <Ionicons name="nutrition-outline" size={24} color={c.muted} />
          </View>
        )}
        <View style={{ flex: 1, gap: 2 }}>
          <Text style={{ color: c.text, fontSize: 16, fontWeight: "600" }} numberOfLines={1}>
            {item.name}
            {item.quantity > 1 ? `  ×${item.quantity}` : ""}
          </Text>
          <Text style={{ color: c.muted, fontSize: 12 }}>
            Added by {item.addedBy}
            {item.price !== null ? ` · ${formatMoney(item.price)}` : ""}
          </Text>
        </View>
        <View style={{ backgroundColor: badge.bg, borderRadius: 6, paddingHorizontal: 8, paddingVertical: 4 }}>
          <Text style={{ color: badge.fg, fontSize: 12, fontWeight: "600" }}>{shortExpiry(item.expiryDate, level)}</Text>
        </View>
      </View>

      {open && (
        <View style={{ gap: 8 }}>
          <Text style={{ color: c.muted, fontSize: 13 }}>
            {expiryLabel(item.expiryDate)}
            {item.expiryDate ? ` (${formatDisplayDate(item.expiryDate)})` : ""}
          </Text>
          <View style={{ flexDirection: "row", gap: 8 }}>
            <ActionButton label={item.quantity > 1 ? "Use one" : "Used up"} color={c.success} onPress={() => onAction("use")} />
            <ActionButton label="Throw out" color="#F59E0B" onPress={() => onAction("throw")} />
            <ActionButton label="Delete" color={c.danger} onPress={() => onAction("delete")} />
          </View>
        </View>
      )}
    </Pressable>
  );
}

function ActionButton({ label, color, onPress }: { label: string; color: string; onPress: () => void }) {
  return (
    <Pressable
      onPress={onPress}
      style={({ pressed }) => [
        { flex: 1, borderRadius: 8, paddingVertical: 10, alignItems: "center", backgroundColor: color },
        pressed && { opacity: 0.7 },
      ]}
    >
      <Text style={{ color: "#fff", fontWeight: "600" }}>{label}</Text>
    </Pressable>
  );
}

function shortExpiry(date: string | null, level: ExpiryLevel) {
  if (!date) return "–";
  if (level === "expired") return "Expired";
  const label = expiryLabel(date);
  return label.replace("Expires ", "").replace("in ", "");
}

function badgeColors(level: ExpiryLevel, c: Colors) {
  switch (level) {
    case "expired":
      return { bg: c.danger, fg: "#fff" };
    case "soon":
      return { bg: "#F59E0B", fg: "#fff" };
    case "ok":
      return { bg: c.border, fg: c.text };
    default:
      return { bg: "transparent", fg: c.muted };
  }
}

const makeStyles = (c: Colors) =>
  StyleSheet.create({
    center: { flex: 1, alignItems: "center", justifyContent: "center" },
    list: { padding: 16, gap: 10, paddingBottom: 100 },
    empty: { color: c.muted, textAlign: "center", marginTop: 48 },
    notice: { color: c.text, backgroundColor: c.card, padding: 12, borderRadius: 10 },
    fab: {
      position: "absolute",
      right: 20,
      bottom: 20,
      width: 60,
      height: 60,
      borderRadius: 30,
      backgroundColor: c.accent,
      alignItems: "center",
      justifyContent: "center",
      elevation: 4,
      shadowColor: "#000",
      shadowOpacity: 0.2,
      shadowRadius: 6,
      shadowOffset: { width: 0, height: 3 },
    },
  });
