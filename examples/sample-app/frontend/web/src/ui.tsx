import type { ReactNode } from "react";

// The WEB impl of the app's design system (`@/ui`) — react-dom primitives. Same names + shape as mobile/src/ui.tsx
// (React Native); the agnostic View in core (Items.view) imports `@/ui` and renders unchanged on either platform.
// The View reaches the design system through these names ONLY — it never imports `react-native` directly, which is
// what keeps it design-system-driven and trivially testable in jsdom (this impl is what the framework check runs).

export function Screen({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <div data-ui="screen" className={className}>
      {children}
    </div>
  );
}

export function Stack({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <div data-ui="stack" className={className}>
      {children}
    </div>
  );
}

export function Text({ children, variant }: { children: ReactNode; variant?: string }) {
  return <span data-variant={variant}>{children}</span>;
}

export function EmptyState({ title, description }: { title: string; description?: string }) {
  return (
    <div data-ui="empty">
      <strong>{title}</strong>
      {description ? <p>{description}</p> : null}
    </div>
  );
}
