// Frontend generators — the parallel of the backend scaffold. Beyond the typed client (orval), these emit the
// *structure*: a feature unit (ViewModel + View + test + i18n) from a name, and the assembled i18n resource tree
// from every feature's catalog. Pure render functions (testable, no I/O); the *.mjs CLIs wrap them with file
// writes, and the `lazuli` .NET CLI front-door shells out to those CLIs (the way `lazuli doctor` shells out to
// `npm run lint`). The unit they emit is the blessed sample/items with names substituted — conformant by
// construction (it passes the LZFE rules + typecheck + its own test, because the sample does).

/** "user-profile" / "userProfiles" -> "UserProfiles" */
export function pascal(s) {
  return String(s)
    .split(/[^a-zA-Z0-9]+/)
    .filter(Boolean)
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join("");
}

/** "Bookings" -> "bookings" */
export function camel(s) {
  const p = pascal(s);
  return p.charAt(0).toLowerCase() + p.slice(1);
}

/** Naive singularize for the entity type — scaffold convenience, the author refines. bookings->booking, ies->y. */
export function singular(s) {
  const w = String(s);
  if (/ies$/i.test(w)) return w.replace(/ies$/i, "y");
  if (/s$/i.test(w) && !/ss$/i.test(w)) return w.replace(/s$/i, "");
  return w;
}

/** A JS-identifier-safe token for a namespace ("user-profile" -> "user_profile"). */
export function ident(ns) {
  return String(ns).replace(/[^a-zA-Z0-9]/g, "_");
}

/**
 * Render a feature unit from a plural feature name (e.g. "bookings"). Returns { filename: contents } for the four
 * co-located files — the canonical unit. Names are derived: ns "bookings", component "Bookings", collection
 * "bookings", entity "Booking", model hook "useBookingsModel", list hook "useListBookings".
 */
export function renderFeature(nameRaw) {
  const Plural = pascal(nameRaw); // "Bookings"
  const collection = camel(Plural); // "bookings"
  const Entity = pascal(singular(camel(nameRaw))); // "Booking"
  const lower = collection.toLowerCase(); // i18n namespace + client.gen segment

  const viewModel = `import { toAsyncState, type AsyncState } from "@lazuli/react";
// The orval-generated typed hook for the \`list_${lower}\` slice — the ONLY data the door touches.
import { useList${Plural} } from "@/client.gen/${lower}";
import i18n from "@/i18n";

// FEATURE UNIT — the ViewModel (the "data door", the front-side of a backend [Slice]). Only place that touches the
// generated client (LZFE002), platform-agnostic so it tests in jsdom (LZFE009), exposes its resource as
// AsyncState<T> (the spine) so the View handles every state by construction.

export interface ${Entity} {
  id: string;
  name: string;
}

export interface ${Plural}Model {
  state: { ${collection}: AsyncState<${Entity}[]> };
}

export function use${Plural}Model(): ${Plural}Model {
  const query = useList${Plural}();

  const ${collection} = toAsyncState<${Entity}[]>(
    {
      isPending: query.isPending,
      isError: query.isError,
      data: query.data?.${collection},
      refetch: query.refetch,
    },
    { errorMessage: i18n.t("${lower}:error"), isEmpty: (list) => list.length === 0 },
  );

  return { state: { ${collection} } };
}
`;

  const view = `import { useTranslation } from "react-i18next";
import { Resource } from "@lazuli/react";
// The design system — the View reaches it through these names only (never react-native directly).
import { Screen, Stack, Text, EmptyState } from "@/ui";
import { use${Plural}Model } from "./${Plural}.viewModel";
import type { ${Entity} } from "./${Plural}.viewModel";

// VIEW — render only (LZFE001). Consumes the resource through <Resource>, so loading / error / empty are handled by
// construction and the body only ever runs with resolved data. No isPending/isError here (LZFE010).
export function ${Plural}View() {
  const { t } = useTranslation("${lower}");
  const { state } = use${Plural}Model();

  return (
    <Resource
      state={state.${collection}}
      empty={
        <Screen>
          <EmptyState title={t("empty.title")} description={t("empty.description")} />
        </Screen>
      }
    >
      {(${collection}) => <${Plural}List ${collection}={${collection}} />}
    </Resource>
  );
}

function ${Plural}List({ ${collection} }: { ${collection}: ${Entity}[] }) {
  return (
    <Screen>
      <Stack className="gap-3">
        {${collection}.map((item) => (
          <Text key={item.id} variant="body">
            {item.name}
          </Text>
        ))}
      </Stack>
    </Screen>
  );
}
`;

  const test = `import type { ReactNode } from "react";
import { describe, it, expect } from "vitest";
import { render, renderHook } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { use${Plural}Model } from "./${Plural}.viewModel";
import { ${Plural}View } from "./${Plural}.view";

// CANONICAL TESTS — the two co-located tiers the harness enforces:
//  - LZFE005 (unit): renderHook the ViewModel (the data door) against the real client — wired, not mocked.
//  - LZFE006 (integration): render the View so it composes with its ViewModel + design system and mounts.
// Neither asserts behavior beyond "it mounts" — behavior stays per-screen judgment.
function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}

describe("${Plural}", () => {
  it("starts its resource in loading while the list is fetched (LZFE005)", () => {
    const { result } = renderHook(() => use${Plural}Model(), { wrapper });
    expect(result.current.state.${collection}.status).toBe("loading");
  });

  it("renders the View without crashing (LZFE006)", () => {
    const { container } = render(<${Plural}View />, { wrapper });
    expect(container).toBeTruthy();
  });
});
`;

  const i18n = `// Feature-scoped copy. Three locales with identical keys (LZFE011) — fill in the real strings.
export const ptBR = {
  error: "Não foi possível carregar.",
  "empty.title": "Nada por aqui ainda",
  "empty.description": "O que você criar aparece aqui.",
} as const;

export const esES = {
  error: "No pudimos cargar.",
  "empty.title": "Nada por aquí todavía",
  "empty.description": "Lo que crees aparecerá aquí.",
} as const;

export const enUS = {
  error: "We couldn't load.",
  "empty.title": "Nothing here yet",
  "empty.description": "What you create will show up here.",
} as const;
`;

  return {
    [`${Plural}.viewModel.ts`]: viewModel,
    [`${Plural}.view.tsx`]: view,
    [`${Plural}.test.tsx`]: test,
    [`${lower}.i18n.ts`]: i18n,
  };
}

/**
 * Render the assembled i18n resource tree from the discovered feature catalogs. `features` is a list of
 * { ns, importPath } (importPath relative to the output file, extensionless). The emitted module imports each
 * locale catalog and composes `resources` keyed by locale -> namespace — the thing the harness wired by hand.
 */
export function renderResources(features) {
  const sorted = [...features].sort((a, b) => a.ns.localeCompare(b.ns));
  const imports = sorted
    .map(
      (f) =>
        `import { ptBR as ${ident(f.ns)}_ptBR, esES as ${ident(f.ns)}_esES, enUS as ${ident(f.ns)}_enUS } from "${f.importPath}";`,
    )
    .join("\n");
  const locale = (suffix) => sorted.map((f) => `    ${JSON.stringify(f.ns)}: ${ident(f.ns)}_${suffix},`).join("\n");

  return `// GENERATED by tools/assemble-i18n.mjs — do not edit. Re-run to regenerate after adding/removing a feature.
${imports}

export const resources = {
  en: {
${locale("enUS")}
  },
  pt: {
${locale("ptBR")}
  },
  es: {
${locale("esES")}
  },
} as const;
`;
}
