import type { ReactNode } from "react";

// Harness stand-ins for the app's design system (`@/ui`). In a real lazuli-net app these wrap the RN / RN-web
// design system; here they are the minimum needed so the blessed sample compiles and its test runs in jsdom. The
// sample reaches the design system through these names ONLY — it never imports `react-native` directly, which is
// the convention that keeps a View design-system-driven and trivially testable on web.

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
