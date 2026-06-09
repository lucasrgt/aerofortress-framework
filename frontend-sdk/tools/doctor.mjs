// LZFE doctor — the aggregation core behind the single front-door that captures "the whole crew" in one pass.
// A consumer (e.g. an app's `scripts/lzfe-doctor.mjs`) does the I/O — runs `eslint --format json`, resolves each
// rule's configured level via `eslint --print-config`, and runs the script-doctors (LZFE008 endpoint coverage,
// LZFE-E2E, LZFE-JOURNEY) — then feeds the raw results here. `aggregateReport` is PURE (no I/O), so it is unit-
// testable and the same core powers both the human dashboard and the `--json` machine output.
//
// Why one report: a frontend has many lint surfaces (LZFE architecture rules, the community kit, expo's set) plus
// the fullstack loops. Scattered across four commands they are easy to half-read. Aggregated and BUCKETED, a
// warn->error promotion becomes an evidence-backed move: you can see, in one place, which rules are gated, which
// are a revealed backlog, and which are already clean (0 hits) and therefore ready to promote.

/** The LZFE rule -> code map. Drives the architecture bucket + the clean-roster view. */
export const LZFE_CODES = {
  "lazuli/view-purity": "LZFE001",
  "lazuli/data-door": "LZFE002",
  "lazuli/no-mock": "LZFE003",
  "lazuli/test-colocated": "LZFE005",
  "lazuli/view-integration-test": "LZFE006",
  "lazuli/viewmodel-platform-agnostic": "LZFE009",
  "lazuli/state-completeness": "LZFE010",
  "lazuli/i18n-completeness": "LZFE011",
  "lazuli/design-tokens": "LZFE012",
  "lazuli/mutation-error-handled": "LZFE013",
  "lazuli/no-hardcoded-copy": "LZFE014",
  "lazuli/no-router-replace-in-effect": "LZFE015",
  "lazuli/session-one-door": "LZFE016",
  "lazuli/guard-tristate": "LZFE017",
  "lazuli/route-param-guard": "LZFE018",
  "lazuli/safe-back": "LZFE019",
  "lazuli/no-hardcoded-base-url": "LZFE020",
  "lazuli/no-raw-html": "LZFE021",
  "lazuli/no-open-redirect": "LZFE022",
  "lazuli/ui-door": "LZFE024",
  "lazuli/scale-only": "LZFE025",
  "lazuli/semantic-colors": "LZFE026",
  "lazuli/query-client-defaults": "LZFE027",
  "lazuli/no-manual-refetch-ritual": "LZFE028",
};

/**
 * Which dashboard bucket a fired rule belongs to.
 * @param {string} ruleId
 * @returns {"lzfe"|"community"|"platform"|"parse"}
 */
export function bucket(ruleId) {
  if (!ruleId) return "parse";
  if (ruleId.startsWith("lazuli/")) return "lzfe";
  if (
    ruleId.startsWith("@tanstack/") ||
    ruleId.startsWith("no-secrets/") ||
    ruleId.startsWith("sonarjs/") ||
    ruleId.startsWith("@typescript-eslint/") ||
    ruleId.startsWith("@vitest/") ||
    ruleId.startsWith("vitest/")
  )
    return "community";
  return "platform"; // expo / react-hooks / import / core
}

/**
 * Aggregate an `eslint --format json` run + the script-doctor outputs into one structured report.
 * @param {object} input
 * @param {{ filePath: string, messages: { ruleId: string|null, severity: number }[] }[]} input.eslintResults
 * @param {Record<string,"error"|"warn"|"off">} [input.ruleLevels] - configured level per rule (incl. 0-hit rules)
 * @param {Record<string,string>} [input.loops] - script-doctor summary lines (endpoint / e2e / journey)
 */
export function aggregateReport({ eslintResults, ruleLevels = {}, loops = {} }) {
  const byRule = {};
  let errors = 0;
  let warnings = 0;
  for (const f of eslintResults) {
    for (const m of f.messages) {
      const id = m.ruleId || "(parse error)";
      (byRule[id] ??= { error: 0, warn: 0, files: new Set() });
      if (m.severity === 2) {
        byRule[id].error++;
        errors++;
      } else {
        byRule[id].warn++;
        warnings++;
      }
      byRule[id].files.add(f.filePath);
    }
  }

  const rules = {};
  for (const [id, v] of Object.entries(byRule)) {
    rules[id] = {
      error: v.error,
      warn: v.warn,
      files: v.files.size,
      bucket: bucket(id),
      level: ruleLevels[id] ?? "?",
      code: LZFE_CODES[id],
    };
  }

  // LZFE rules with 0 hits — the clean roster. A `warn`-level clean rule is a promotion candidate; an `error`-level
  // one is already a gate proving its invariant holds.
  const cleanLzfe = Object.keys(LZFE_CODES)
    .filter((id) => !byRule[id])
    .map((id) => ({ id, code: LZFE_CODES[id], level: ruleLevels[id] ?? "?" }));

  return {
    summary: { errors, warnings, rules: Object.keys(byRule).length },
    rules,
    cleanLzfe,
    loops,
    ok: errors === 0,
  };
}
