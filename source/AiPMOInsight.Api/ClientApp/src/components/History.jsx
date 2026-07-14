import { useEffect, useState } from 'react';
import { authFetch } from '../AuthContext';

const EMPTY_VIEW = { uploadId: '', findings: [], narrative: [], challenge: [], review: [] };

// Read-only history: the left column lists every upload (newest first); selecting one loads that
// upload's latest analysis run and renders the same four sections as the Level-2 project view —
// findings, narrative, challenge, review. View-only: no re-analyze, no delete.
export function History() {
  const [uploads, setUploads] = useState([]);
  const [selectedId, setSelectedId] = useState(null);
  const [view, setView] = useState(EMPTY_VIEW);
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
    try {
      const res = await authFetch(`/api/uploads/${id}/findings`);
      if (!res.ok) throw new Error(`GET /api/uploads/${id}/findings failed (${res.status})`);
      setView({ ...EMPTY_VIEW, ...(await res.json()) });
    } catch (e) {
      setError(e.message);
      setView(EMPTY_VIEW);
    } finally {
      setDetailLoading(false);
    }
  }

  const hasAnything =
    view.findings.length + view.narrative.length + view.challenge.length + view.review.length > 0;

  return (
    <div>
      <h1>Upload history</h1>
      <p>Every uploaded file and its latest analysis. Select an upload to see its findings.</p>

      {error && <p style={{ color: 'var(--pico-del-color)' }}>{error}</p>}

      <div className="grid">
        <aside>
          <h2>Uploads</h2>
          {listLoading ? (
            <p aria-busy="true">Loading…</p>
          ) : uploads.length === 0 ? (
            <p><em>Nothing uploaded yet.</em></p>
          ) : (
            <ul style={{ listStyle: 'none', padding: 0 }}>
              {uploads.map(u => (
                <li key={u.id}>
                  <a
                    href="#"
                    aria-current={u.id === selectedId ? 'true' : undefined}
                    onClick={e => { e.preventDefault(); selectUpload(u.id); }}
                  >
                    {u.fileName}
                    <br />
                    <small>{new Date(u.uploadedAt).toLocaleString()}</small>
                  </a>
                </li>
              ))}
            </ul>
          )}
        </aside>

        <section>
          {!selectedId ? (
            <p><em>Select an upload to view its analysis.</em></p>
          ) : detailLoading ? (
            <p aria-busy="true">Loading…</p>
          ) : !hasAnything ? (
            <p><em>This upload has not been analyzed yet.</em></p>
          ) : (
            <>
              <SynthesisSection title="Narrative" items={view.narrative} />
              <FindingsSection findings={view.findings} />
              <SynthesisSection title="Challenge" items={view.challenge} />
              <SynthesisSection title="Review" items={view.review} />
            </>
          )}
        </section>
      </div>
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
