// Journey parity — the fullstack-loop doctor. The backend declares its critical journeys as Journeys/*.Tests.cs;
// the frontend declares its e2e journeys in e2e/flows.json. This proves the two SETS agree, so no critical journey
// is half-built (tested on the back but never end-to-end on the front, or vice-versa). It closes the loop at the
// JOURNEY grain — the endpoint grain is already closed elsewhere (tsc for front->back, AFFE008 for back->front).
//
// Matching is EXPLICIT, never fuzzy: a frontend flow links to a backend journey via a `backendJourney` field
// holding the journey's key (its Journeys filename minus `.Tests.cs`, e.g. "HostOnboardingFlow"). A flow with no
// `backendJourney` is a UI-only journey (allowed — not every front journey has a backend twin). `checkJourneyParity`
// is pure (no I/O); a consumer reads the backend dir + flows.json and feeds it the two lists.
//
// Reports both directions:
//   - uncovered: a backend journey with no frontend flow linking to it
//   - orphans:   a frontend flow whose `backendJourney` names a journey the backend doesn't have

/**
 * @param {string[]} backendJourneys - journey keys (Journeys/<key>.Tests.cs)
 * @param {{ name?: string, backendJourney?: string }[]} frontendFlows - the flows.json entries
 */
export function checkJourneyParity(backendJourneys, frontendFlows) {
  const backend = new Set(backendJourneys);
  const linked = new Set();
  const orphans = [];

  for (const f of frontendFlows) {
    if (!f.backendJourney) continue; // UI-only flow — allowed, not an orphan
    if (backend.has(f.backendJourney)) linked.add(f.backendJourney);
    else orphans.push({ flow: f.name ?? "(unnamed)", backendJourney: f.backendJourney });
  }

  const uncovered = backendJourneys.filter((j) => !linked.has(j));
  const messages = [
    ...uncovered.map((j) => `backend journey "${j}" has no frontend flow (uncovered)`),
    ...orphans.map((o) => `frontend flow "${o.flow}" links backend journey "${o.backendJourney}" which doesn't exist`),
  ];

  return { uncovered, orphans, linked: [...linked], gaps: uncovered.length + orphans.length, messages };
}
