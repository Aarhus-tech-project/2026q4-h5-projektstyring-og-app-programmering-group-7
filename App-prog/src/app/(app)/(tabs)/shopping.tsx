import Ionicons from "@expo/vector-icons/Ionicons";
import { useFocusEffect } from "expo-router";
import { useCallback, useState } from "react";
import { FlatList, Pressable, RefreshControl, Share, StyleSheet, Text, View } from "react-native";

import { useColors, type Colors } from "@/components/theme";
import { Button, ErrorText, Field } from "@/components/ui";
import { useSession } from "@/context/auth";
import { useCurrentHousehold } from "@/context/household";
import { api, type ShoppingItem } from "@/lib/api";

export default function ShoppingScreen() {
  const c = useColors();
  const styles = makeStyles(c);
  const { token } = useSession();
  const { household } = useCurrentHousehold();

  const [items, setItems] = useState<ShoppingItem[]>([]);
  const [newName, setNewName] = useState("");
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      setItems(await api.shopping.list(token!, household.id));
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not load the shopping list.");
    }
  }, [token, household.id]);

  useFocusEffect(
    useCallback(() => {
      load();
    }, [load]),
  );

  async function guarded(action: () => Promise<unknown>) {
    try {
      await action();
      await load();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Something went wrong.");
    }
  }

  async function add() {
    const name = newName.trim();
    if (!name) return;
    setNewName("");
    await guarded(() => api.shopping.add(token!, household.id, name));
  }

  async function toggle(item: ShoppingItem) {
    // Update the UI immediately, then sync with the server
    setItems((list) => list.map((i) => (i.id === item.id ? { ...i, checked: !i.checked } : i)));
    await guarded(() => api.shopping.setChecked(token!, household.id, item.id, !item.checked));
  }

  /** Opens the phone's share sheet (Messages, Messenger, Notes, ...). */
  async function share() {
    const open = items.filter((i) => !i.checked);
    await Share.share({
      title: `Shopping list – ${household.name}`,
      message: `Shopping list for ${household.name}:\n${open.map((i) => `• ${i.name}`).join("\n")}`,
    });
  }

  const openCount = items.filter((i) => !i.checked).length;
  const checkedCount = items.length - openCount;

  return (
    <FlatList
      style={{ backgroundColor: c.background }}
      contentContainerStyle={styles.list}
      data={items}
      keyExtractor={(i) => String(i.id)}
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
      ListHeaderComponent={
        <View style={{ gap: 12 }}>
          <View style={{ flexDirection: "row", gap: 8, alignItems: "flex-end" }}>
            <View style={{ flex: 1 }}>
              <Field placeholder="Add something…" value={newName} onChangeText={setNewName} onSubmitEditing={add} returnKeyType="done" />
            </View>
            <Button label="Add" onPress={add} disabled={!newName.trim()} />
          </View>
          <ErrorText>{error}</ErrorText>
          {openCount > 0 && <Button label={`Share list (${openCount})`} variant="secondary" onPress={share} />}
        </View>
      }
      ListEmptyComponent={
        <Text style={styles.empty}>Nothing to buy. Items you run out of are added here automatically.</Text>
      }
      renderItem={({ item }) => (
        <View style={styles.row}>
          <Pressable onPress={() => toggle(item)} style={styles.rowMain} hitSlop={6}>
            <Ionicons name={item.checked ? "checkbox" : "square-outline"} size={24} color={item.checked ? c.success : c.muted} />
            <Text style={[styles.name, item.checked && styles.checked]}>{item.name}</Text>
          </Pressable>
          <Pressable onPress={() => guarded(() => api.shopping.remove(token!, household.id, item.id))} hitSlop={10}>
            <Ionicons name="close" size={20} color={c.muted} />
          </Pressable>
        </View>
      )}
      ListFooterComponent={
        checkedCount > 0 ? (
          <Button
            label={`Clear ${checkedCount} bought`}
            variant="secondary"
            onPress={() => guarded(() => api.shopping.clearChecked(token!, household.id))}
          />
        ) : undefined
      }
    />
  );
}

const makeStyles = (c: Colors) =>
  StyleSheet.create({
    list: { padding: 16, gap: 8 },
    empty: { color: c.muted, textAlign: "center", marginTop: 32 },
    row: { flexDirection: "row", alignItems: "center", backgroundColor: c.card, borderRadius: 10, padding: 12, gap: 12 },
    rowMain: { flex: 1, flexDirection: "row", alignItems: "center", gap: 12 },
    name: { color: c.text, fontSize: 16, flexShrink: 1 },
    checked: { color: c.muted, textDecorationLine: "line-through" },
  });
