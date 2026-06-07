import type { ReactNode } from "react";
import { View, Text as RNText } from "react-native";

// The MOBILE impl of the app's design system (`@/ui`) — React Native primitives. Same names + shape as
// web/src/ui.tsx; the agnostic View in core (Items.view) imports `@/ui` and renders UNCHANGED on either platform —
// only this implementation differs. This file is the mobile platform shell's territory: it is built with the
// consumer's Expo/Metro toolchain (which provides react-native), NOT by the framework's web-only `npm run check`,
// exactly like a real lazuli-net app's mobile target. core/ + web/ are what the framework verifies in jsdom.

export function Screen({ children }: { children: ReactNode; className?: string }) {
  return <View style={{ flex: 1 }}>{children}</View>;
}

export function Stack({ children }: { children: ReactNode; className?: string }) {
  return <View style={{ gap: 12 }}>{children}</View>;
}

export function Text({ children }: { children: ReactNode; variant?: string }) {
  return <RNText>{children}</RNText>;
}

export function EmptyState({ title, description }: { title: string; description?: string }) {
  return (
    <View>
      <RNText>{title}</RNText>
      {description ? <RNText>{description}</RNText> : null}
    </View>
  );
}
