import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { authFetch } from '../AuthContext';
import { HealthBanner } from './HealthBanner';
import { HEALTH_STATE, bucketColour, healthState } from '../health';
import {
  ChallengeArticle,
  ConfidenceChip,
  NarrativeArticle,
  ReviewArticle,
  renderFindingSummary,
} from '../synthesis';

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
              <NarrativeSection items={view.narrative} />
              <DecisionsSection findings={view.findings} />
              <FindingsSection findings={view.findings.filter(f => f.area !== 'Decision')} />
              <ChallengeSection items={view.challenge} />
              <ReviewSection items={view.review} />

              {/* Upcoming-milestones dedicated panel is the next follow-on (Panel 5); decisions now have a
                  real panel above. Schedule findings still appear in Findings until then. */}
              <section className="block">
                <div className="sec-head">
                  <h2 className="sec-title">Upcoming milestones</h2>
                  <span className="sec-kicker">follow-on</span>
                </div>
                <div className="flagged-panel">
                  <p className="flagged-note">A dedicated dated-milestone view (next 2–4 weeks) is the next Phase 5
                    follow-on. Schedule findings appear under Findings above; nothing is fabricated here.</p>
                </div>
              </section>
            </>
          )}
        </>
      )}
    </div>
  );
}

// Decisions needed (Panel 6): the un-approved decisions the Decision agent flagged (overdue = Red,
// due-soon = Amber). Rendered with structured owner/deadline/consequence carried on each finding's
// metricDetail — columns, not a parsed summary string. Worst-first, then by nearest deadline.
const SEV_RANK = { Red: 3, Amber: 2, Green: 1 };

function DecisionsSection({ findings }) {
  const decisions = findings
    .filter(f => f.area === 'Decision')
    .sort((a, b) =>
      (SEV_RANK[b.severity] ?? 0) - (SEV_RANK[a.severity] ?? 0)
      || (a.metricDetail?.deadline ?? '').localeCompare(b.metricDetail?.deadline ?? ''));

  if (decisions.length === 0) return null;

  return (
    <section className="block">
      <div className="sec-head">
        <h2 className="sec-title">Decisions needed</h2>
        <span className="sec-kicker">{decisions.length} · owner · deadline · consequence · worst first</span>
      </div>
      <table className="records">
        <thead>
          <tr><th>Decision</th><th>Owner</th><th>Deadline</th><th>Consequence</th><th>Status</th></tr>
        </thead>
        <tbody>
          {decisions.map(d => (
            <tr key={d.id} className={`severity ${bucketColour(d.severity)}`}>
              <td><strong>{d.metricDetail?.title ?? d.summary}</strong></td>
              <td>{d.metricDetail?.owner ?? '—'}</td>
              <td>{d.metricDetail?.deadline ?? '—'}</td>
              <td>{d.metricDetail?.consequence || '—'}</td>
              <td><span className={`sev ${bucketColour(d.severity)}`}>{d.severity}</span></td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
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
                <td>{renderFindingSummary(f.summary)}</td>
                <td>{f.producingAgent}</td>
                <td><ConfidenceChip value={f.confidence} /></td>
                <td><span className="cite">{f.citation?.locator}<br />upload {f.citation?.uploadId}</span></td>
              </tr>
            ))
          )}
        </tbody>
      </table>
    </section>
  );
}

function NarrativeSection({ items }) {
  if (items.length === 0) return null;
  return (
    <section className="block">
      <div className="sec-head">
        <h2 className="sec-title">AI recommendation (narrative)</h2>
        <span className="sec-kicker">US 5 · closest recommendation surface</span>
      </div>
      {items.map(item => <NarrativeArticle key={item.id} item={item} />)}
    </section>
  );
}

function ChallengeSection({ items }) {
  if (items.length === 0) return null;
  return (
    <section className="block">
      <div className="sec-head">
        <h2 className="sec-title">Challenge</h2>
        <span className="sec-kicker">US 9 · adversarial critique</span>
      </div>
      {items.map(item => <ChallengeArticle key={item.id} item={item} />)}
    </section>
  );
}

function ReviewSection({ items }) {
  if (items.length === 0) return null;
  return (
    <section className="block">
      <div className="sec-head">
        <h2 className="sec-title">Review</h2>
        <span className="sec-kicker">US 9 · questions before publishing</span>
      </div>
      {items.map(item => <ReviewArticle key={item.id} item={item} />)}
    </section>
  );
}

function formatConfidence(c) {
  if (typeof c !== 'number') return '—';
  return c <= 1 ? `${Math.round(c * 100)}%` : `${Math.round(c)}%`;
}
