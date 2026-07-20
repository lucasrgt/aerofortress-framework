import type { ReactNode } from "react";
import { color, radius, shadow, space, type SpaceToken } from "./tokens-bridge";

// The raised surface — elevation is a token (`shadow.raised`), never a per-card invention.
export function Card({
  children,
  padding = "lg",
  listItem = false,
}: {
  children: ReactNode;
  padding?: SpaceToken;
  /** Maps the collection-row semantic to the platform accessibility tree. */
  listItem?: boolean;
}) {
  return (
    <div
      data-ui="card"
      role={listItem ? "listitem" : undefined}
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
