import { useEffect, useState } from 'react';
import { authFetch } from '../AuthContext';
import { bucketColour, healthState, HEALTH_STATE } from '../health';
import { HealthBanner } from './HealthBanner';
import { uploadStatus, runProvenance, projectKeys, UPLOAD_STATUS } from '../history';
import {
  ChallengeArticle,
  ConfidenceChip,
  NarrativeArticle,
  ReviewArticle,
  renderFindingSummary,
} from '../synthesis';

const EMPTY_VIEW = { uploadId: '', findings: [], narrative: [], challenge: [], review: [] };

// Read-only history rebuilt as a master-detail audit surface (add-history-rich-detail, US-9/US-10):
// a sticky master list of every upload (newest first) + a detail panel for the selected upload's latest
// run — a run-provenance header, the four cited sections (analysis/narrative/challenge/review), and a
// score-audit section that reuses GET /api/projects/{key}/health per project in the run.
//
// Presentation-only boundary (as L1/L2/L3): fields the current reads don't carry — uploader, LLM model,
// project count, multi-file summary, live Running/Failed status — render a flagged follow-on state, never
// fabricated. The score audit is the project's CURRENT health (the health read is per-project-latest-run);
// a strict per-run historical audit is a flagged follow-on.
export function History() {
  const [uploads, setUploads] = useState([]);
  const [selectedId, setSelectedId] = useState(null);
  const [view, setView] = useState(EMPTY_VIEW);
  const [audits, setAudits] = useState([]); // [{ projectKey, state, score }]
  const [listLoading, setListLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    (async () => {
      setListLoading(true);
      setError(null);
      try {
        const res = await authFetch('/api/uploads');
        if (!res.ok) throw new Error(`GET /api/uploads failed (${res.status})`);
        setUploads(await res.json());
      } catch (e) {
        setError(e.message);
      } finally {
        setListLoading(false);
      }
    })();
  }, []);

  async function selectUpload(id) {
    setSelectedId(id);
    setDetailLoading(true);
    setError(null);
    setAudits([]);
    try {
      const res = await authFetch(`/api/uploads/${id}/findings`);
      if (!res.ok) throw new Error(`GET /api/uploads/${id}/findings failed (${res.status})`);
      const v = { ...EMPTY_VIEW, ...(await res.json()) };
      setView(v);

      // Score audit (US-10): fetch health per distinct project key, independently — one 404/failure
      // never blanks the section (Promise.allSettled). Reuses the existing health endpoint (no backend).
      const keys = projectKeys(v);
      const settled = await Promise.allSettled(keys.map(loadHealth));
      setAudits(settled.filter(r => r.status === 'fulfilled').map(r => r.value));
    } catch (e) {
      setError(e.message);
      setView(EMPTY_VIEW);
    } finally {
      setDetailLoading(false);
    }
  }

  async function loadHealth(projectKey) {
    try {
      const res = await authFetch(`/api/projects/${encodeURIComponent(projectKey)}/health`);
      const body = res.ok ? await res.json() : null;
      return { projectKey, state: healthState(res.status, body), score: body?.score ?? null };
    } catch {
      return { projectKey, state: HEALTH_STATE.ERROR, score: null };
    }
  }

  const status = uploadStatus(view);
  const analyzed = status === UPLOAD_STATUS.ANALYZED;
  const prov = runProvenance(view);

  return (
    <div>
      <p className="eyebrow"><span className="num">History</span> Audit trail</p>
      <h1>What the AI said, and why.</h1>
      <p>Every upload and its latest analysis run — findings, how they were challenged and reviewed, and the
        score audit — each item cited to its source. Read-only.</p>

      {error && <p style={{ color: 'var(--pico-del-color)' }}>{error}</p>}

      <div className="history-layout">
        {/* ── Master list ───────────────────────────────────────────────────────────────────── */}
        <aside className="history-master">
          <div className="sec-head">
            <h2 className="sec-title">Uploads</h2>
            <span className="sec-kicker">Newest first</span>
          </div>
          {listLoading ? (
            <p aria-busy="true">Loading…</p>
          ) : uploads.length === 0 ? (
            <p><em>Nothing uploaded yet.</em></p>
          ) : (
            <ul className="upload-list">
              {uploads.map(u => (
                <li key={u.id}>
                  <button
                    type="button"
                    className={`upload-row${u.id === selectedId ? ' selected' : ''}`}
                    aria-current={u.id === selectedId ? 'true' : undefined}
                    onClick={() => selectUpload(u.id)}
                  >
                    <span className="upload-file">{u.fileName}</span>
                    <span className="upload-date">{new Date(u.uploadedAt).toLocaleString()}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
          <p className="flagged-note">
            Uploader, project count, a multi-file summary, and a live Running/Failed status aren’t captured
            yet — a follow-on. Rows show only what the list read returns.
          </p>
        </aside>

        {/* ── Detail panel ──────────────────────────────────────────────────────────────────── */}
        <section className="history-detail">
          {!selectedId ? (
            <p><em>Select an upload to view its analysis.</em></p>
          ) : detailLoading ? (
            <p aria-busy="true">Loading…</p>
          ) : !analyzed ? (
            <p><em>This upload has not been analyzed yet.</em></p>
          ) : (
            <>
              <RunHeader prov={prov} />
              <NarrativeSection items={view.narrative} />
              <FindingsSection findings={view.findings} />
              <ChallengeSection items={view.challenge} />
              <ReviewSection items={view.review} />
              <ScoreAudit audits={audits} />
            </>
          )}
        </section>
      </div>
    </div>
  );
}

// Run-provenance header — everything here comes from the findings response itself (run id, prompt hash,
// date). The uploader and LLM model are not in the response → flagged, never fabricated.
function RunHeader({ prov }) {
  return (
    <div className="run-header">
      <div className="run-meta">
        <div><span className="summary-label">Run</span><code>{prov.runId ?? '—'}</code></div>
        <div>
          <span className="summary-label">Date</span>
          {prov.createdAt ? new Date(prov.createdAt).toLocaleString() : '—'}
        </div>
        <div>
          <span className="summary-label">Prompt hash</span>
          {prov.promptHashes.length === 0
            ? <span className="flagged">none (deterministic run)</span>
            : prov.promptHashes.map(h => <code key={h} className="hash">{h.slice(0, 18)}…</code>)}
        </div>
      </div>
      <p className="flagged-note">
        Uploader and LLM model aren’t carried by the findings read — a follow-on. Not shown as if known.
      </p>
    </div>
  );
}

// KPI findings table (analysis agents) on the shared records/sev idioms.
function FindingsSection({ findings }) {
  return (
    <section className="block">
      <div className="sec-head">
        <h2 className="sec-title">Analysis</h2>
        <span className="sec-kicker">{findings.length} finding{findings.length === 1 ? '' : 's'} · cited</span>
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
                <td><span className="cite">↳ {f.citation?.locator}</span></td>
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
        <h2 className="sec-title">Narrative</h2>
        <span className="sec-kicker">Agent #7</span>
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
        <span className="sec-kicker">Agent #8</span>
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
        <span className="sec-kicker">Agent #9</span>
      </div>
      {items.map(item => <ReviewArticle key={item.id} item={item} />)}
    </section>
  );
}

// Score audit (US-10) — reuses the per-project health read. Labelled as the project's CURRENT health,
// with the explicit caveat that a strict per-run historical audit is a follow-on.
function ScoreAudit({ audits }) {
  return (
    <section className="block">
      <div className="sec-head">
        <h2 className="sec-title">Score audit</h2>
        <span className="sec-kicker">Current project health · cited overrides</span>
      </div>
      <p className="flagged-note">
        Shows each project’s <strong>current</strong> health (the health read scores the project’s latest
        run). For an upload whose run is no longer the latest, a strict per-run historical audit is a
        follow-on.
      </p>
      {audits.length === 0 ? (
        <p><em>No project health available for this run.</em></p>
      ) : (
        audits.map(a => (
          <div key={a.projectKey} className="audit-project">
            <div className="sec-head">
              <h3 className="sec-title" style={{ fontSize: '1rem' }}>
                <span className={`sev ${bucketColour(a.score?.finalBucket)}`}>
                  {a.score?.finalBucket ?? '—'}
                </span>{' '}
                {a.projectKey}
              </h3>
            </div>
            <HealthBanner state={a.state} score={a.score} />
          </div>
        ))
      )}
    </section>
  );
}
