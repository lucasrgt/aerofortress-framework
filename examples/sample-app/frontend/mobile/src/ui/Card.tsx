import type { ReactNode } from "react";
import { View } from "react-native";
import { color, radius, space, type SpaceToken } from "./tokens-bridge";

// The web shadow tokens are box-shadow strings; RN expresses elevation natively — this is the map.
const ELEVATION = { none: 0, raised: 2, overlay: 8 } as const;

export function Card({
  children,
  padding = "lg",
  listItem = false,
}: {
  children: ReactNode;
  padding?: SpaceToken;
  listItem?: boolean;
}) {
  return (
    <View
      role={listItem ? "listitem" : undefined}
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
