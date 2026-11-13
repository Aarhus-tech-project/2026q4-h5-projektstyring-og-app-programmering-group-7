import { use, type PropsWithChildren } from "react";
import { KeyboardAvoidingView, Platform, ScrollView, type ScrollViewProps } from "react-native";
import { HeaderHeightContext } from "expo-router/react-navigation";

/**
 * Scrollable form that keeps the focused text field visible above the keyboard.
 *
 * - iOS: the ScrollView adjusts its insets for the keyboard and scrolls to the
 *   focused field by itself.
 * - Android: Expo apps are edge-to-edge, so the window no longer shrinks when the
 *   keyboard opens. KeyboardAvoidingView adds bottom padding instead, offset by the
 *   navigation header height (which includes the status bar).
 *
 * (react-native-keyboard-controller would be smoother, but it isn't in Expo Go.)
 */
export function KeyboardScroll({ children, ...scrollProps }: PropsWithChildren<ScrollViewProps>) {
  // 0 on screens without a header; reading the context directly avoids
  // useHeaderHeight() throwing outside a navigator.
  const headerHeight = use(HeaderHeightContext) ?? 0;

  const scroll = (
    <ScrollView
      keyboardShouldPersistTaps="handled"
      keyboardDismissMode={Platform.OS === "ios" ? "interactive" : "on-drag"}
      automaticallyAdjustKeyboardInsets={Platform.OS === "ios"}
      {...scrollProps}
    >
      {children}
    </ScrollView>
  );

  if (Platform.OS !== "android") return scroll;

  return (
    <KeyboardAvoidingView style={{ flex: 1 }} behavior="padding" keyboardVerticalOffset={headerHeight}>
      {scroll}
    </KeyboardAvoidingView>
  );
}
