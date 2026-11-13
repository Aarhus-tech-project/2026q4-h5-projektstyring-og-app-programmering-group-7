import { Alert, Platform } from "react-native";

/** Yes/no dialog that works on iOS, Android and web. Resolves true when confirmed. */
export function confirm(title: string, message: string, confirmLabel = "OK", destructive = false): Promise<boolean> {
  if (Platform.OS === "web") return Promise.resolve(window.confirm(`${title}\n\n${message}`));
  return new Promise((resolve) =>
    Alert.alert(title, message, [
      { text: "Cancel", style: "cancel", onPress: () => resolve(false) },
      { text: confirmLabel, style: destructive ? "destructive" : "default", onPress: () => resolve(true) },
    ], { cancelable: true, onDismiss: () => resolve(false) }),
  );
}
