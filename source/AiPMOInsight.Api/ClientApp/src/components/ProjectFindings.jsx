import { useState } from 'react';
import { authFetch } from '../AuthContext';
import { HealthBanner } from './HealthBanner';
import { HEALTH_STATE, healthState } from '../health';

const EMPTY_VIEW = { projectKey: '', findings: [], narrative: [], challenge: [], review: [] };
const EMPTY_HEALTH = { state: HEALTH_STATE.ERROR, score: null };

// The Data Collector (UploadParser) only parses these formats. CSV is intentionally NOT supported —
// a .csv upload parses to nothing and yields zero findings, so we reject it up front rather than let
// the user hit a silent empty result.
const ACCEPTED_EXTENSIONS = ['.xlsx', '.xlsm', '.xml', '.docx'];
const ACCEPT_ATTR = ACCEPTED_EXTENSIONS.join(',');
const isAcceptedFile = name => ACCEPTED_EXTENSIONS.some(ext => name.toLowerCase().endsWith(ext));

// Level-2 (individual project status) view. Reads the four analysis sections for a project key —
// KPI findings, the synthesised narrative, the adversarial challenge, and the anticipated review
// questions — each cited back to its source. The uploader exercises the full upload -> analyze ->
// read flow from the UI.
export function ProjectFindings() {
  const [projectKey, setProjectKey] = useState('ALPHA');
  const [view, setView] = useState(EMPTY_VIEW);
  const [health, setHealth] = useState(EMPTY_HEALTH);
  const [file, setFile] = useState(null);
  const [status, setStatus] = useState(null);
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

  function onFileChange(e) {
    const picked = e.target.files?.[0] ?? null;
    if (picked && !isAcceptedFile(picked.name)) {
      setError(`Unsupported file type "${picked.name}". Upload ${ACCEPTED_EXTENSIONS.join(', ')} — CSV is not supported.`);
      setFile(null);
      e.target.value = ''; // let the user re-pick the same-named file after fixing it
      return;
    }
    setError(null);
    setFile(picked);
  }

  async function uploadAnalyzeRead(e) {
    e.preventDefault();
    if (!file) return;
    if (!isAcceptedFile(file.name)) { // defensive: state should already prevent this
      setError(`Unsupported file type "${file.name}". Upload ${ACCEPTED_EXTENSIONS.join(', ')} — CSV is not supported.`);
      return;
    }
    setError(null);
    setStatus('Uploading…');
    try {
      const form = new FormData();
      form.append('file', file);
      const up = await authFetch('/api/ingest/upload', { method: 'POST', body: form });
      if (!up.ok) throw new Error(`upload failed (${up.status})`);
      const { uploadId } = await up.json();

      setStatus('Analyzing…');
      const an = await authFetch(`/api/analyze/${uploadId}`, { method: 'POST' });
      if (!an.ok) throw new Error(`analyze failed (${an.status})`);
      const analyzed = await an.json();
      const key = analyzed.findings?.[0]?.projectKey ?? projectKey;
      setProjectKey(key);

      setStatus('Loading findings…');
      await loadFindings(key);
      setStatus('Done.');
    } catch (e2) {
      setError(e2.message);
      setStatus(null);
    }
  }

  const hasAnything =
    view.findings.length + view.narrative.length + view.challenge.length + view.review.length > 0;

  return (
    <div>
      <h1>Project status (Level 2)</h1>
      <p>Upload a project export, analyze it, and read the findings, narrative, challenge, and review — every item cites the source it came from.</p>

      <form onSubmit={uploadAnalyzeRead} role="group">
        <input type="file" accept={ACCEPT_ATTR} aria-label="Project export" onChange={onFileChange} />
        <button type="submit" disabled={!file}>Upload → analyze → read</button>
      </form>
      <p><small>Accepted formats: {ACCEPTED_EXTENSIONS.join(', ')}. CSV is not supported.</small></p>

      <form onSubmit={e => { e.preventDefault(); loadFindings(projectKey); }} role="group">
        <input
          type="text"
          value={projectKey}
          placeholder="Project key"
          aria-label="Project key"
          onChange={e => setProjectKey(e.target.value)}
        />
        <button type="submit">Load</button>
      </form>

      {status && <p><small>{status}</small></p>}
      {error && <p style={{ color: 'var(--pico-del-color)' }}>{error}</p>}

      {loading ? (
        <p aria-busy="true">Loading…</p>
      ) : (
        <>
          <HealthBanner state={health.state} score={health.score} />
          {!hasAnything ? (
            <p><em>No analysis recorded for this project key yet.</em></p>
          ) : (
            <>
              <SynthesisSection title="Narrative" items={view.narrative} />
              <FindingsSection findings={view.findings} />
              <SynthesisSection title="Challenge" items={view.challenge} />
              <SynthesisSection title="Review" items={view.review} />
              <p className="l2-gap-note">
                <small>
                  Dated upcoming milestones, per-decision owner/deadline, and an explicit AI recommendation
                  are not yet captured in the finding shape — the Narrative above is the closest recommendation
                  surface. These are a Phase 5 follow-on, not rendered here.
                </small>
              </p>
            </>
          )}
        </>
      )}
    </div>
  );
}

// KPI findings table (analysis agents).
function FindingsSection({ findings }) {
  return (
    <section>
      <h2>Findings ({findings.length})</h2>
      <table>
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
                <td>{f.confidence}</td>
                <td><small>{f.citation?.locator}<br />upload {f.citation?.uploadId}</small></td>
              </tr>
            ))
          )}
        </tbody>
      </table>
    </section>
  );
}

// Narrative / Challenge / Review — one synthesised finding each, rendered as prose.
function SynthesisSection({ title, items }) {
  if (items.length === 0) return null;
  return (
    <section>
      <h2>{title}</h2>
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
