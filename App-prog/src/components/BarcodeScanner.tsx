import { CameraView, useCameraPermissions, type BarcodeScanningResult } from "expo-camera";
import { useRef } from "react";
import { StyleSheet, Text, View } from "react-native";

import { useColors } from "@/components/theme";
import { Button } from "@/components/ui";

// Grocery barcodes (EAN/UPC). Limiting the types makes detection faster.
const BARCODE_TYPES = ["ean13", "ean8", "upc_a", "upc_e"] as const;

/** Camera view that reports the first grocery barcode it sees. */
export function BarcodeScanner({ onScanned, onCancel }: { onScanned: (barcode: string) => void; onCancel: () => void }) {
  const c = useColors();
  const [permission, requestPermission] = useCameraPermissions();
  // The camera fires many events per second; only handle the first one
  const handled = useRef(false);

  if (!permission) return null;

  if (!permission.granted) {
    return (
      <View style={[styles.box, { backgroundColor: c.card, padding: 16, gap: 12, justifyContent: "center" }]}>
        <Text style={{ color: c.text, textAlign: "center" }}>
          FridgeShare needs the camera to scan barcodes.
        </Text>
        {permission.canAskAgain ? (
          <Button label="Allow camera" onPress={requestPermission} />
        ) : (
          <Text style={{ color: c.muted, textAlign: "center" }}>Enable camera access in your phone&apos;s settings.</Text>
        )}
        <Button label="Cancel" variant="secondary" onPress={onCancel} />
      </View>
    );
  }

  function handle(result: BarcodeScanningResult) {
    if (handled.current) return;
    handled.current = true;
    onScanned(result.data);
  }

  return (
    <View style={{ gap: 8 }}>
      <View style={styles.box}>
        <CameraView
          style={StyleSheet.absoluteFill}
          facing="back"
          barcodeScannerSettings={{ barcodeTypes: [...BARCODE_TYPES] }}
          onBarcodeScanned={handle}
        />
        <View style={styles.frame} pointerEvents="none" />
      </View>
      <Button label="Cancel scan" variant="secondary" onPress={onCancel} />
    </View>
  );
}

const styles = StyleSheet.create({
  box: { height: 260, borderRadius: 12, overflow: "hidden" },
  frame: {
    position: "absolute",
    left: "12%",
    right: "12%",
    top: "35%",
    bottom: "35%",
    borderWidth: 2,
    borderColor: "#fff",
    borderRadius: 8,
  },
});
