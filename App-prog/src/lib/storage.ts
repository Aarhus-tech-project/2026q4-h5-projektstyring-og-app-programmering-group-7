import * as SecureStore from "expo-secure-store";
import { Platform } from "react-native";

// SecureStore is native-only, so fall back to localStorage on web.

export async function getItem(key: string): Promise<string | null> {
  if (Platform.OS === "web") {
    try {
      return typeof localStorage !== "undefined" ? localStorage.getItem(key) : null;
    } catch {
      return null;
    }
  }
  return SecureStore.getItemAsync(key);
}

export async function setItem(key: string, value: string | null): Promise<void> {
  if (Platform.OS === "web") {
    try {
      if (value === null) localStorage.removeItem(key);
      else localStorage.setItem(key, value);
    } catch {
      // storage unavailable (private mode etc.) — session just won't persist
    }
    return;
  }
  if (value === null) await SecureStore.deleteItemAsync(key);
  else await SecureStore.setItemAsync(key, value);
}
