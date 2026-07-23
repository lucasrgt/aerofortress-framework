// AFFE doctor — the aggregation core behind the single front-door that captures "the whole crew" in one pass.
// A consumer (e.g. an app's `scripts/affe-doctor.mjs`) does the I/O — runs `eslint --format json`, resolves each
// rule's configured level via `eslint --print-config`, and runs the script-doctors (AFFE008 endpoint coverage,
// AFFE-E2E, AFFE-JOURNEY) — then feeds the raw results here. `aggregateReport` is PURE (no I/O), so it is unit-
// testable and the same core powers both the human dashboard and the `--json` machine output.
//
// Why one report: a frontend has many lint surfaces (AFFE architecture rules, the community kit, expo's set) plus
// the fullstack loops. Scattered across four commands they are easy to half-read. Aggregated and BUCKETED, a
// warn->error promotion becomes an evidence-backed move: you can see, in one place, which rules are gated, which
// are a revealed backlog, and which are already clean (0 hits) and therefore ready to promote.

/** The AFFE rule -> code map. Drives the architecture bucket + the clean-roster view. */
export const AFFE_CODES = {
  "aerofortress/view-purity": "AFFE001",
  "aerofortress/data-door": "AFFE002",
  "aerofortress/no-mock": "AFFE003",
  "aerofortress/test-colocated": "AFFE005",
  "aerofortress/view-integration-test": "AFFE006",
  "aerofortress/viewmodel-platform-agnostic": "AFFE009",
  "aerofortress/state-completeness": "AFFE010",
  "aerofortress/i18n-completeness": "AFFE011",
  "aerofortress/design-tokens": "AFFE012",
  "aerofortress/mutation-error-handled": "AFFE013",
  "aerofortress/no-hardcoded-copy": "AFFE014",
  "aerofortress/no-router-replace-in-effect": "AFFE015",
  "aerofortress/session-one-door": "AFFE016",
  "aerofortress/guard-tristate": "AFFE017",
  "aerofortress/route-param-guard": "AFFE018",
  "aerofortress/safe-back": "AFFE019",
  "aerofortress/no-hardcoded-base-url": "AFFE020",
  "aerofortress/no-raw-html": "AFFE021",
  "aerofortress/no-open-redirect": "AFFE022",
  "aerofortress/ui-door": "AFFE024",
  "aerofortress/scale-only": "AFFE025",
  "aerofortress/semantic-colors": "AFFE026",
  "aerofortress/query-client-defaults": "AFFE027",
  "aerofortress/no-manual-refetch-ritual": "AFFE028",
  "aerofortress/refresh-one-door": "AFFE029",
  "aerofortress/no-cast-navigation": "AFFE030",
  "aerofortress/submit-handles-invalid": "AFFE031",
  "aerofortress/controller-field-state": "AFFE032",
  "aerofortress/verify-has-avp-proof": "AFFE033",
  "aerofortress/no-disabled-tests": "AFFE034",
  "aerofortress/feature-has-e2e-flow": "AFFE035",
};

/**
 * Which dashboard bucket a fired rule belongs to.
 * @param {string} ruleId
 * @returns {"affe"|"community"|"platform"|"parse"}
 */
export function bucket(ruleId) {
  if (!ruleId) return "parse";
  if (ruleId.startsWith("aerofortress/")) return "affe";
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
      code: AFFE_CODES[id],
    };
  }

  // AFFE rules with 0 hits — the clean roster. A `warn`-level clean rule is a promotion candidate; an `error`-level
  // one is already a gate proving its invariant holds.
  const cleanAffe = Object.keys(AFFE_CODES)
    .filter((id) => !byRule[id])
    .map((id) => ({ id, code: AFFE_CODES[id], level: ruleLevels[id] ?? "?" }));

  return {
    summary: { errors, warnings, rules: Object.keys(byRule).length },
    rules,
    cleanAffe,
    loops,
    ok: errors === 0,
  };
}
