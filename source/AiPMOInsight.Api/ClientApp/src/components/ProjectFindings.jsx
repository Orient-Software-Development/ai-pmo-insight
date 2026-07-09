import { useState } from 'react';
import { authFetch } from '../AuthContext';

// Level-2 (individual project status) view. Reads findings for a project key and shows each
// finding's citation back to its source — the skeleton's trust link. The uploader below exercises
// the full upload -> analyze -> read flow from the UI.
export function ProjectFindings() {
  const [projectKey, setProjectKey] = useState('DUMMY-001');
  const [findings, setFindings] = useState([]);
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
      setFindings(await res.json());
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

  return (
    <div>
      <h1>Project findings (Level 2)</h1>
      <p>Read the findings recorded for a project key. Every finding cites the source it came from.</p>

      <form onSubmit={uploadAnalyzeRead} role="group">
        <input
          type="file"
          aria-label="Fixture file"
          onChange={e => setFile(e.target.files?.[0] ?? null)}
        />
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
      ) : (
        <table>
          <thead>
            <tr><th>Summary</th><th>Cited source</th><th>Created</th></tr>
          </thead>
          <tbody>
            {findings.length === 0 ? (
              <tr><td colSpan={3}><em>No findings for this project key.</em></td></tr>
            ) : (
              findings.map(f => (
                <tr key={f.id}>
                  <td>{f.summary}</td>
                  <td><small>{f.citation?.locator} <br />upload {f.citation?.uploadId}</small></td>
                  <td>{new Date(f.createdAt).toLocaleString()}</td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      )}
    </div>
  );
}
