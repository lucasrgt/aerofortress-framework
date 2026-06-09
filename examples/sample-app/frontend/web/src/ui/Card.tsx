import type { ReactNode } from "react";
import { color, radius, shadow, space, type SpaceToken } from "./tokens-bridge";

// The raised surface — elevation is a token (`shadow.raised`), never a per-card invention.
export function Card({ children, padding = "lg" }: { children: ReactNode; padding?: SpaceToken }) {
  return (
    <div
      data-ui="card"
      style={{
        backgroundColor: color.surface,
        borderWidth: 1,
        borderStyle: "solid",
        borderColor: color.border,
        borderRadius: radius.lg,
        boxShadow: shadow.raised,
        padding: space[padding],
      }}
    >
      {children}
    </div>
  );
}
