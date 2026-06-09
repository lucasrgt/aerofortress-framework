import type { ReactNode } from "react";
import { View } from "react-native";
import { color, radius, space, type SpaceToken } from "./tokens-bridge";

// The web shadow tokens are box-shadow strings; RN expresses elevation natively — this is the map.
const ELEVATION = { none: 0, raised: 2, overlay: 8 } as const;

export function Card({ children, padding = "lg" }: { children: ReactNode; padding?: SpaceToken }) {
  return (
    <View
      style={{
        backgroundColor: color.surface,
        borderWidth: 1,
        borderColor: color.border,
        borderRadius: radius.lg,
        elevation: ELEVATION.raised,
        padding: space[padding],
      }}
    >
      {children}
    </View>
  );
}
