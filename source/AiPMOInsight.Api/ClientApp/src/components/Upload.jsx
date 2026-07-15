import { useState } from 'react';
import { Link } from 'react-router-dom';
import { authFetch } from '../AuthContext';

// Ingest / cold-start page (add-analyze-flow-and-l2-retrofit), built to the v2 wireframe
// (docs/designs/phase5-wireframe-v2.html, data-page="upload"). Hosts the upload -> analyze flow that
// used to live at the top of the Level-2 project view; post-login lands here.
//
// Presentation-only boundary (same as the L1/L2 views): the backend returns an upload id and a completed
// analysis result — it does NOT return per-file parse status/notes, duplicate-identity detection (US-2),
// or a live per-agent progress stream (US-9). Those wireframe panels render as clear "not yet captured —
// follow-on" placeholders; the pipeline panel shows the coarse request lifecycle only. Nothing fabricated.

// The Data Collector (UploadParser) only parses these formats. CSV is intentionally NOT supported — a
// .csv upload parses to nothing and yields zero findings, so it is rejected up front (see the
// csv-parsing-deferred decision) rather than letting the user hit a silent empty result.
const ACCEPTED_EXTENSIONS = ['.xlsx', '.xlsm', '.xml', '.docx'];
const ACCEPT_ATTR = ACCEPTED_EXTENSIONS.join(',');
const isAcceptedFile = name => ACCEPTED_EXTENSIONS.some(ext => name.toLowerCase().endsWith(ext));

// The plan-doc / wireframe nine-agent pipeline. Rendered as a labelled placeholder stepper: the analyze
// endpoint is a single request with no per-agent telemetry, so we do not fake per-agent ticks.
const PIPELINE_AGENTS = [
  '01 Data Collector', '02 Data Quality', '03 Status', '04 Risk & Issue', '05 Financial',
  '06 Resource', '07 Narrative', '08 Challenge', '09 Review',
];

// Coarse request-lifecycle phases (design Decision 4) — this is what the API actually tells us.
const PHASE = { IDLE: 'idle', UPLOADING: 'uploading', ANALYZING: 'analyzing', DONE: 'done', FAILED: 'failed' };

const prettyBytes = n => (n < 1024 * 1024 ? `${Math.round(n / 1024)} KB` : `${(n / (1024 * 1024)).toFixed(1)} MB`);

export function Upload() {
  const [file, setFile] = useState(null);
  const [phase, setPhase] = useState(PHASE.IDLE);
  const [error, setError] = useState(null);
  const [dragOver, setDragOver] = useState(false);
  const [result, setResult] = useState(null); // { uploadId, projectKey }

  function pickFile(picked) {
    if (!picked) return;
    if (!isAcceptedFile(picked.name)) {
      setError(`Unsupported file type "${picked.name}". Upload ${ACCEPTED_EXTENSIONS.join(', ')} — CSV is not supported.`);
      setFile(null);
      return;
    }
    setError(null);
    setResult(null);
    setPhase(PHASE.IDLE);
    setFile(picked);
  }

  function onDrop(e) {
    e.preventDefault();
    setDragOver(false);
    pickFile(e.dataTransfer.files?.[0] ?? null);
  }

  async function runFlow(e) {
    e.preventDefault();
    if (!file || !isAcceptedFile(file.name)) return;
    setError(null);
    setResult(null);
    try {
      setPhase(PHASE.UPLOADING);
      const form = new FormData();
      form.append('file', file);
      const up = await authFetch('/api/ingest/upload', { method: 'POST', body: form });
      if (!up.ok) throw new Error(`upload failed (${up.status})`);
      const { uploadId } = await up.json();

      setPhase(PHASE.ANALYZING);
      const an = await authFetch(`/api/analyze/${uploadId}`, { method: 'POST' });
      if (!an.ok) throw new Error(`analyze failed (${an.status})`);
      const analyzed = await an.json();
      const projectKey = analyzed.findings?.[0]?.projectKey ?? null;

      setResult({ uploadId, projectKey });
      setPhase(PHASE.DONE);
    } catch (err) {
      setError(err.message);
      setPhase(PHASE.FAILED);
    }
  }

  const busy = phase === PHASE.UPLOADING || phase === PHASE.ANALYZING;

  return (
    <div>
      <p className="eyebrow"><span className="num">US 1</span> Ingest · project data</p>
      <h1>Upload the client's project files.</h1>
      <p>Drop the Orbit export and any supporting minutes or risk registers. The pipeline parses them and
        runs the nine analysis agents; every finding it produces cites the source it came from.</p>

      {/* ── Drop zone (BACKED: single-file upload) ──────────────────────────────────────────── */}
      <form onSubmit={runFlow}>
        <label
          className={`dropzone${dragOver ? ' over' : ''}`}
          onDragOver={e => { e.preventDefault(); setDragOver(true); }}
          onDragLeave={() => setDragOver(false)}
          onDrop={onDrop}
        >
          <input
            type="file"
            accept={ACCEPT_ATTR}
            aria-label="Project export"
            onChange={e => pickFile(e.target.files?.[0] ?? null)}
            hidden
          />
          <span className="dropzone-main">Drop a file here, or click to browse</span>
          <span className="dropzone-sub">One Orbit workbook or supporting minutes / risk register · {ACCEPTED_EXTENSIONS.join(' · ')}</span>
          <span className="dropzone-sub flagged-note">Multi-file batch upload is a follow-on — one file per run for now.</span>
        </label>

        {error && <p style={{ color: 'var(--pico-del-color)' }}>{error}</p>}

        {/* ── This upload · parse results (BACKED file identity; FLAGGED per-file parse detail) ── */}
        {file && (
          <section className="block">
            <div className="sec-head">
              <h2 className="sec-title">This upload</h2>
              <span className="sec-kicker">{prettyBytes(file.size)}</span>
            </div>
            <table className="records">
              <thead>
                <tr><th>File</th><th>Status</th><th>Extracted</th><th>Notes</th></tr>
              </thead>
              <tbody>
                <tr>
                  <td><strong>{file.name}</strong></td>
                  <td><span className="sev rag-none">{phaseLabel(phase)}</span></td>
                  <td className="flagged-note">—</td>
                  <td className="flagged-note">Per-file parse status, extracted counts, and column-mapping
                    flags are not yet returned by the API — follow-on.</td>
                </tr>
              </tbody>
            </table>
          </section>
        )}

        <button type="submit" disabled={!file || busy} aria-busy={busy}>
          Run analysis →
        </button>
      </form>

      {/* ── Analysis pipeline (BACKED coarse lifecycle; FLAGGED live per-agent progress) ─────── */}
      {phase !== PHASE.IDLE && (
        <section className="block">
          <div className="sec-head">
            <h2 className="sec-title">Analysis pipeline</h2>
            <span className="sec-kicker">{phaseLabel(phase)}</span>
          </div>
          <div className="pipeline">
            {PIPELINE_AGENTS.map(a => (
              <span key={a} className={`pipeline-step${phase === PHASE.DONE ? ' done' : ''}`}>{a}</span>
            ))}
          </div>
          <p className="flagged-note">Live per-agent progress (US-9) is not yet streamed — the analyze call
            returns once the whole run completes, so this shows the request lifecycle, not per-agent ticks.</p>
        </section>
      )}

      {/* ── Duplicate identity (FLAGGED: US-2, no detection surface yet) ─────────────────────── */}
      <section className="block">
        <div className="sec-head">
          <h2 className="sec-title">Duplicate project identity</h2>
          <span className="sec-kicker">US 2</span>
        </div>
        <div className="flagged-panel">
          <p className="flagged-note">Duplicate-identity detection and the merge / keep-separate decision are
            not yet captured — a follow-on. The layout is reserved here with no fabricated candidates.</p>
        </div>
      </section>

      {result && (
        <p className="analyze-done">
          <strong>Analysis complete.</strong>{' '}
          {result.projectKey
            ? <Link to={`/projects?key=${encodeURIComponent(result.projectKey)}`}>View {result.projectKey} results →</Link>
            : <Link to="/projects">View results →</Link>}
        </p>
      )}
    </div>
  );
}

function phaseLabel(phase) {
  switch (phase) {
    case PHASE.UPLOADING: return 'Uploading…';
    case PHASE.ANALYZING: return 'Analyzing…';
    case PHASE.DONE: return 'Done';
    case PHASE.FAILED: return 'Failed';
    default: return 'Selected';
  }
}
