import { useState } from 'react';
import { authFetch } from '../AuthContext';

const EMPTY_VIEW = { projectKey: '', findings: [], narrative: [], challenge: [], review: [] };

// Level-2 (individual project status) view. Reads the four analysis sections for a project key —
// KPI findings, the synthesised narrative, the adversarial challenge, and the anticipated review
// questions — each cited back to its source. The uploader exercises the full upload -> analyze ->
// read flow from the UI.
export function ProjectFindings() {
  const [projectKey, setProjectKey] = useState('ALPHA');
  const [view, setView] = useState(EMPTY_VIEW);
  const [file, setFile] = useState(null);
  const [status, setStatus] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(false);

  async function loadFindings(key) {
    setLoading(true);
    setError(null);
    try {
      const res = await authFetch(`/api/projects/${encodeURIComponent(key)}`);
      if (!res.ok) throw new Error(`GET /api/projects/${key} failed (${res.status})`);
      setView({ ...EMPTY_VIEW, ...(await res.json()) });
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  }

  async function uploadAnalyzeRead(e) {
    e.preventDefault();
    if (!file) return;
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
        <input type="file" aria-label="Project export" onChange={e => setFile(e.target.files?.[0] ?? null)} />
        <button type="submit" disabled={!file}>Upload → analyze → read</button>
      </form>

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
      ) : !hasAnything ? (
        <p><em>No analysis recorded for this project key yet.</em></p>
      ) : (
        <>
          <SynthesisSection title="Narrative" items={view.narrative} />
          <FindingsSection findings={view.findings} />
          <SynthesisSection title="Challenge" items={view.challenge} />
          <SynthesisSection title="Review" items={view.review} />
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
