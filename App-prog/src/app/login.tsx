import { useState } from "react";
import {
  ActivityIndicator,
  Pressable,
  StyleSheet,
  Text,
  TextInput,
  useColorScheme,
  View,
} from "react-native";

import { KeyboardScroll } from "@/components/KeyboardScroll";
import { useSession } from "@/context/auth";

type Mode = "login" | "register";

export default function Login() {
  const { signIn, signUp } = useSession();
  const dark = useColorScheme() === "dark";
  const colors = dark ? darkColors : lightColors;

  const [mode, setMode] = useState<Mode>("login");
  const [email, setEmail] = useState("");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const isRegister = mode === "register";
  const canSubmit = email.trim() && password && (!isRegister || username.trim()) && !submitting;

  async function handleSubmit() {
    if (!canSubmit) return;
    setError(null);
    setSubmitting(true);
    try {
      if (isRegister) await signUp(email.trim(), username.trim(), password);
      else await signIn(email.trim(), password);
      // On success the guard in _layout switches to the app automatically.
    } catch (e) {
      setError(e instanceof Error ? e.message : "Something went wrong.");
      setSubmitting(false);
    }
  }

  function switchMode() {
    setMode(isRegister ? "login" : "register");
    setError(null);
  }

  const inputStyle = [styles.input, { backgroundColor: colors.input, color: colors.text, borderColor: colors.border }];

  return (
    <View style={[styles.flex, { backgroundColor: colors.background }]}>
      <KeyboardScroll contentContainerStyle={styles.container}>
        <View style={styles.card}>
          <Text style={[styles.title, { color: colors.text }]}>
            {isRegister ? "Create account" : "Welcome back"}
          </Text>
          <Text style={[styles.subtitle, { color: colors.muted }]}>
            {isRegister ? "Sign up to get started." : "Log in to your account."}
          </Text>

          <TextInput
            style={inputStyle}
            placeholder="Email"
            placeholderTextColor={colors.muted}
            value={email}
            onChangeText={setEmail}
            autoCapitalize="none"
            autoComplete="email"
            keyboardType="email-address"
            textContentType="emailAddress"
            returnKeyType="next"
          />

          {isRegister && (
            <TextInput
              style={inputStyle}
              placeholder="Username"
              placeholderTextColor={colors.muted}
              value={username}
              onChangeText={setUsername}
              autoCapitalize="none"
              autoComplete="username"
              textContentType="username"
              returnKeyType="next"
            />
          )}

          <TextInput
            style={inputStyle}
            placeholder={isRegister ? "Password (min. 8 characters)" : "Password"}
            placeholderTextColor={colors.muted}
            value={password}
            onChangeText={setPassword}
            secureTextEntry
            autoComplete={isRegister ? "new-password" : "current-password"}
            textContentType={isRegister ? "newPassword" : "password"}
            returnKeyType="go"
            onSubmitEditing={handleSubmit}
          />

          {error && <Text style={styles.error}>{error}</Text>}

          <Pressable
            style={({ pressed }) => [styles.button, (!canSubmit || pressed) && styles.buttonDim]}
            onPress={handleSubmit}
            disabled={!canSubmit}
          >
            {submitting ? (
              <ActivityIndicator color="#fff" />
            ) : (
              <Text style={styles.buttonText}>{isRegister ? "Sign up" : "Log in"}</Text>
            )}
          </Pressable>

          <Pressable onPress={switchMode} style={styles.switch}>
            <Text style={{ color: colors.muted }}>
              {isRegister ? "Already have an account? " : "Don't have an account? "}
              <Text style={styles.link}>{isRegister ? "Log in" : "Sign up"}</Text>
            </Text>
          </Pressable>
        </View>
      </KeyboardScroll>
    </View>
  );
}

const lightColors = { background: "#F5F7FA", input: "#FFFFFF", text: "#111", muted: "#6B7280", border: "#D1D5DB" };
const darkColors = { background: "#0B0F14", input: "#161B22", text: "#F3F4F6", muted: "#9CA3AF", border: "#30363D" };

const styles = StyleSheet.create({
  flex: { flex: 1 },
  container: { flexGrow: 1, justifyContent: "center", padding: 24 },
  card: { width: "100%", maxWidth: 400, alignSelf: "center", gap: 12 },
  title: { fontSize: 28, fontWeight: "700" },
  subtitle: { fontSize: 16, marginBottom: 12 },
  input: { borderWidth: 1, borderRadius: 10, paddingHorizontal: 14, paddingVertical: 12, fontSize: 16 },
  error: { color: "#DC2626", fontSize: 14 },
  button: {
    marginTop: 8,
    height: 48,
    borderRadius: 10,
    backgroundColor: "#208AEF",
    alignItems: "center",
    justifyContent: "center",
  },
  buttonDim: { opacity: 0.6 },
  buttonText: { color: "#fff", fontSize: 16, fontWeight: "600" },
  switch: { alignItems: "center", paddingVertical: 8 },
  link: { color: "#208AEF", fontWeight: "600" },
});
