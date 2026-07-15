import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { authFetch } from '../AuthContext';
import { HealthBanner } from './HealthBanner';
import { HEALTH_STATE, bucketColour, healthState } from '../health';

const EMPTY_VIEW = { projectKey: '', findings: [], narrative: [], challenge: [], review: [] };
const EMPTY_HEALTH = { state: HEALTH_STATE.ERROR, score: null };

// Level-2 (individual project status) view, retrofitted to the v2 wireframe (data-page="l2") in
// add-analyze-flow-and-l2-retrofit. Reads the four analysis sections for a project key — KPI findings,
// the synthesised narrative, the adversarial challenge, and the anticipated review questions — each cited
// back to its source, plus the RAG health score, and presents them in the shared Phase 5 design system.
//
// The upload -> analyze flow now lives on its own /upload page; a "view results" link there hands the
// analyzed project key to this view via ?key=. The data path (concurrent findings + health read, the
// healthState mapping, the four sections) is unchanged and locked by ProjectStatusDashboardDataTests.
//
// Presentation-only boundary: dated upcoming milestones, per-decision owner/deadline/consequence, and an
// explicit AI recommendation exceed the current finding shape — rendered as flagged follow-ons, never
// fabricated. Sponsor/PM are shown only if the surface carries them (it does not yet).
export function ProjectFindings() {
  const [searchParams] = useSearchParams();
  const [projectKey, setProjectKey] = useState(searchParams.get('key') ?? 'ALPHA');
  const [view, setView] = useState(EMPTY_VIEW);
  const [health, setHealth] = useState(EMPTY_HEALTH);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(false);

  // Reads both Level-2 surfaces for a key concurrently: the findings sections and the health score.
  // The two are independent — a health 404/null-score never blanks the findings, and a findings error
  // never blanks a resolved banner (each state is set from its own response).
  async function loadFindings(key) {
    setLoading(true);
    setError(null);
    setHealth(EMPTY_HEALTH);
    const [findingsResult, healthResult] = await Promise.allSettled([
      authFetch(`/api/projects/${encodeURIComponent(key)}`),
      authFetch(`/api/projects/${encodeURIComponent(key)}/health`),
    ]);

    try {
      if (findingsResult.status !== 'fulfilled') throw findingsResult.reason;
      const res = findingsResult.value;
      if (!res.ok) throw new Error(`GET /api/projects/${key} failed (${res.status})`);
      setView({ ...EMPTY_VIEW, ...(await res.json()) });
    } catch (e) {
      setError(e.message);
    }

    // Health is best-effort and orthogonal to findings: map its response to a render state; on any
    // fetch failure fall back to ERROR (the banner renders nothing, the findings still show).
    if (healthResult.status === 'fulfilled') {
      const hres = healthResult.value;
      let body = null;
      try { body = hres.status === 200 ? await hres.json() : null; } catch { body = null; }
      setHealth({ state: healthState(hres.status, body), score: body?.score ?? null });
    } else {
      setHealth(EMPTY_HEALTH);
    }

    setLoading(false);
  }

  // Auto-load when arriving with ?key= (the hand-off from /upload) or on first mount for the default key.
  useEffect(() => {
    loadFindings(projectKey);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const hasAnything =
    view.findings.length + view.narrative.length + view.challenge.length + view.review.length > 0;

  const score = health.state === HEALTH_STATE.SCORED ? health.score : null;
  const overridden = score && score.finalBucket !== score.rawBucket;

  return (
    <div>
      {/* ── Project header (identity + RAG headline + switcher) ──────────────────────────────── */}
      <div className="l2-header">
        <p className="eyebrow"><span className="num">L2</span> Project Detail</p>
        <div className="l2-title-row">
          <h1>{view.projectKey || projectKey}</h1>
          {score && (
            <span className={`sev ${bucketColour(score.finalBucket)}`}>{score.finalBucket}</span>
          )}
          {score && (
            <span className="conf">confidence {formatConfidence(score.confidence)}</span>
          )}
          {overridden && <span className="l2-overridden" title="A worst-case floor override changed the raw bucket">score overridden</span>}
        </div>

        <form className="l2-switcher" onSubmit={e => { e.preventDefault(); loadFindings(projectKey); }} role="group">
          <input
            type="text"
            value={projectKey}
            placeholder="Project key"
            aria-label="Switch project"
            onChange={e => setProjectKey(e.target.value)}
          />
          <button type="submit">Switch project</button>
        </form>
      </div>

      {error && <p style={{ color: 'var(--pico-del-color)' }}>{error}</p>}

      {loading ? (
        <p aria-busy="true">Loading…</p>
      ) : (
        <>
          {/* Health headline + score audit (area breakdown, confidence, override trail, PM-review flag). */}
          <HealthBanner state={health.state} score={health.score} />

          {!hasAnything ? (
            <p><em>No analysis recorded for this project key yet.</em></p>
          ) : (
            <>
              <SynthesisSection title="AI recommendation (narrative)" kicker="US 5 · closest recommendation surface" items={view.narrative} />
              <FindingsSection findings={view.findings} />
              <SynthesisSection title="Challenge" kicker="US 9 · adversarial critique" items={view.challenge} />
              <SynthesisSection title="Review" kicker="US 9 · questions before publishing" items={view.review} />

              {/* Wireframe l2 panels the finding shape does not carry — flagged, not fabricated. */}
              <section className="block">
                <div className="sec-head">
                  <h2 className="sec-title">Milestones &amp; decisions</h2>
                  <span className="sec-kicker">follow-on</span>
                </div>
                <div className="flagged-panel">
                  <p className="flagged-note">Dated upcoming milestones and per-decision owner/deadline/consequence
                    are not yet captured in the finding shape — a Phase 5 follow-on. The Narrative above is the
                    closest recommendation surface; nothing is fabricated here.</p>
                </div>
              </section>
            </>
          )}
        </>
      )}
    </div>
  );
}

// KPI findings table (analysis agents) — "Risks & Issues" in the wireframe.
function FindingsSection({ findings }) {
  return (
    <section className="block">
      <div className="sec-head">
        <h2 className="sec-title">Findings</h2>
        <span className="sec-kicker">{findings.length} · each cites its source</span>
      </div>
      <table className="records">
        <thead>
          <tr><th>Finding</th><th>Agent</th><th>Confidence</th><th>Cited source</th></tr>
        </thead>
        <tbody>
          {findings.length === 0 ? (
            <tr><td colSpan={4}><em>No analytic findings.</em></td></tr>
          ) : (
            findings.map(f => (
              <tr key={f.id}>
                <td>{f.summary}</td>
                <td>{f.producingAgent}</td>
                <td><span className="conf">{f.confidence}</span></td>
                <td><span className="cite">{f.citation?.locator}<br />upload {f.citation?.uploadId}</span></td>
              </tr>
            ))
          )}
        </tbody>
      </table>
    </section>
  );
}

// Narrative / Challenge / Review — one synthesised finding each, rendered as prose.
function SynthesisSection({ title, kicker, items }) {
  if (items.length === 0) return null;
  return (
    <section className="block">
      <div className="sec-head">
        <h2 className="sec-title">{title}</h2>
        {kicker && <span className="sec-kicker">{kicker}</span>}
      </div>
      {items.map(item => (
        <article key={item.id}>
          <p style={{ whiteSpace: 'pre-wrap', margin: 0 }}>{item.summary}</p>
          <footer>
            <small>
              confidence: {item.confidence}
              {item.promptVersion ? ` · prompt ${item.promptVersion.slice(0, 14)}…` : ' · template (no LLM)'}
              {' · '}cites {item.citation?.locator}
            </small>
          </footer>
        </article>
      ))}
    </section>
  );
}

function formatConfidence(c) {
  if (typeof c !== 'number') return '—';
  return c <= 1 ? `${Math.round(c * 100)}%` : `${Math.round(c)}%`;
}
