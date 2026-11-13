import { Image } from "expo-image";
import { router } from "expo-router";
import { useState } from "react";
import { Platform, Pressable, StyleSheet, Text, View } from "react-native";

import { BarcodeScanner } from "@/components/BarcodeScanner";
import { KeyboardScroll } from "@/components/KeyboardScroll";
import { useColors, type Colors } from "@/components/theme";
import { BudgetBar, Button, Card, ErrorText, Field } from "@/components/ui";
import { useSession } from "@/context/auth";
import { useCurrentHousehold } from "@/context/household";
import { api, ApiError, isOverBudget, type NewItem } from "@/lib/api";
import { budgetCheckMessage, checkPrice } from "@/lib/budget";
import { confirm } from "@/lib/confirm";
import { addDays, EXPIRY_PRESETS, formatDisplayDate, parseDisplayDate, toIsoDate } from "@/lib/expiry";
import { parseMoney } from "@/lib/format";

export default function AddItemScreen() {
  const c = useColors();
  const styles = makeStyles(c);
  const { token } = useSession();
  const { household, budget, refreshBudget } = useCurrentHousehold();

  const [scanning, setScanning] = useState(false);
  const [lookingUp, setLookingUp] = useState(false);
  const [barcode, setBarcode] = useState<string | null>(null);
  const [imageUrl, setImageUrl] = useState<string | null>(null);
  const [name, setName] = useState("");
  const [quantity, setQuantity] = useState(1);
  /** What the user sees/types: "dd-mm-yyyy". Converted to YYYY-MM-DD for the API. */
  const [expiry, setExpiry] = useState("");
  const expiryIso = parseDisplayDate(expiry);
  const [priceText, setPriceText] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);

  const price = parseMoney(priceText);
  const priceInvalid = priceText.trim() !== "" && price === null;
  const expiryInvalid = expiry.trim() !== "" && expiryIso === null;
  const budgetCheck = checkPrice(budget, price);
  const budgetMessage = price ? budgetCheckMessage(budgetCheck) : null;
  const canSave = name.trim() && !priceInvalid && !expiryInvalid && !saving;

  async function onScanned(code: string) {
    setScanning(false);
    setBarcode(code);
    setError(null);
    setLookingUp(true);
    try {
      const product = await api.products.lookup(token!, code);
      setName(product.brand && !product.name.includes(product.brand) ? `${product.name} (${product.brand})` : product.name);
      setImageUrl(product.imageUrl);
      setInfo(`Found: ${product.name}`);
    } catch (e) {
      setInfo(e instanceof ApiError && e.status === 404 ? "Product not found. Type the name yourself." : null);
      if (!(e instanceof ApiError && e.status === 404)) setError(e instanceof Error ? e.message : "Lookup failed.");
    } finally {
      setLookingUp(false);
    }
  }

  async function save(allowOverBudget = false) {
    if (!canSave) return;
    setError(null);
    setSaving(true);
    const item: NewItem = {
      name: name.trim(),
      barcode,
      imageUrl,
      quantity,
      expiryDate: expiryIso,
      price,
      allowOverBudget,
    };
    try {
      await api.items.create(token!, household.id, item);
      await refreshBudget();
      router.back();
    } catch (e) {
      setSaving(false);
      // The server's budget check said no: show its numbers and let the user decide
      if (isOverBudget(e)) {
        const ok = await confirm("Over budget", `${e.body.message}\n\nAdd it anyway?`, "Add anyway");
        if (ok) await save(true);
        else await refreshBudget(); // someone else may have spent in the meantime
        return;
      }
      setError(e instanceof Error ? e.message : "Could not save the item.");
    }
  }

  return (
    <KeyboardScroll style={{ backgroundColor: c.background }} contentContainerStyle={styles.content}>
        {Platform.OS !== "web" &&
          (scanning ? (
            <BarcodeScanner onScanned={onScanned} onCancel={() => setScanning(false)} />
          ) : (
            <Button label={barcode ? `Scanned ${barcode} · scan again` : "Scan barcode"} variant="secondary" onPress={() => setScanning(true)} loading={lookingUp} />
          ))}
        {info && <Text style={{ color: c.muted }}>{info}</Text>}

        <Card>
          <View style={{ flexDirection: "row", gap: 12, alignItems: "flex-end" }}>
            {imageUrl && <Image source={imageUrl} style={{ width: 56, height: 56, borderRadius: 8 }} contentFit="cover" />}
            <View style={{ flex: 1 }}>
              <Field label="Name" placeholder="e.g. Milk" value={name} onChangeText={setName} />
            </View>
          </View>

          <View style={{ gap: 6 }}>
            <Text style={styles.label}>Quantity</Text>
            <View style={{ flexDirection: "row", alignItems: "center", gap: 16 }}>
              <Stepper label="−" onPress={() => setQuantity((q) => Math.max(1, q - 1))} colors={c} />
              <Text style={{ color: c.text, fontSize: 20, fontWeight: "700", minWidth: 32, textAlign: "center" }}>{quantity}</Text>
              <Stepper label="+" onPress={() => setQuantity((q) => Math.min(999, q + 1))} colors={c} />
            </View>
          </View>

          <View style={{ gap: 6 }}>
            <Text style={styles.label}>Expiry date</Text>
            <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}>
              {EXPIRY_PRESETS.map((p) => {
                const value = formatDisplayDate(toIsoDate(addDays(new Date(), p.days)));
                const active = expiry === value;
                return (
                  <Pressable key={p.label} onPress={() => setExpiry(active ? "" : value)} style={[styles.chip, active && styles.chipActive]}>
                    <Text style={{ color: active ? "#fff" : c.text }}>{p.label}</Text>
                  </Pressable>
                );
              })}
            </View>
            <Field
              placeholder="or type dd-mm-yyyy"
              value={expiry}
              onChangeText={setExpiry}
              autoCorrect={false}
              keyboardType="numbers-and-punctuation"
            />
            {expiryInvalid && (
              <ErrorText>Use dd-mm-yyyy, e.g. {formatDisplayDate(toIsoDate(addDays(new Date(), 5)))}</ErrorText>
            )}
          </View>

          <Field label="Price (optional)" placeholder="e.g. 12,95" value={priceText} onChangeText={setPriceText} keyboardType="decimal-pad" />
          {priceInvalid && <ErrorText>Enter a price like 12,95</ErrorText>}
          {budgetMessage && (
            <Text style={{ color: budgetCheck.kind === "over" ? c.danger : budgetCheck.kind === "near" ? "#F59E0B" : c.muted }}>
              {budgetMessage}
            </Text>
          )}
        </Card>

        {budget && budget.monthlyBudget !== null && (
          <Card>
            <BudgetBar budget={budget} />
          </Card>
        )}

        <ErrorText>{error}</ErrorText>
        <Button
          label={budgetCheck.kind === "over" && price ? "Add (over budget)" : "Add to fridge"}
          variant={budgetCheck.kind === "over" && price ? "danger" : "primary"}
          onPress={() => save(false)}
          disabled={!canSave}
          loading={saving}
        />
    </KeyboardScroll>
  );
}

function Stepper({ label, onPress, colors: c }: { label: string; onPress: () => void; colors: Colors }) {
  return (
    <Pressable
      onPress={onPress}
      style={({ pressed }) => [
        { width: 44, height: 44, borderRadius: 22, borderWidth: 1, borderColor: c.border, alignItems: "center", justifyContent: "center" },
        pressed && { opacity: 0.6 },
      ]}
    >
      <Text style={{ color: c.text, fontSize: 22 }}>{label}</Text>
    </Pressable>
  );
}

const makeStyles = (c: Colors) =>
  StyleSheet.create({
    content: { padding: 16, gap: 16, paddingBottom: 40 },
    label: { color: c.muted, fontSize: 13, fontWeight: "600" },
    chip: { borderWidth: 1, borderColor: c.border, borderRadius: 16, paddingHorizontal: 12, paddingVertical: 6 },
    chipActive: { backgroundColor: c.accent, borderColor: c.accent },
  });
