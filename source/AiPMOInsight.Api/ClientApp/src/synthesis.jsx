// Shared L2 / History synthesis rendering — parses the LLM's severity brackets ([red]/[medium]/…) out
// of finding text and renders them as .sev chips instead of literal `[word]` prefixes. Also normalises
// the confidence field and the "Risk [Critical]" / "Issue [High]" pattern used in the KPI findings
// table.
//
// Presentation-only: no backend / API / finding-shape change. The shipped LLM prompts emit these
// severity tags in the summary text; this module keeps the raw text intact for cite/debug while
// giving the reader a clean layout.

const SEVERITY_MAP = {
  red: { cls: 'rag-red', label: 'Red' },
  amber: { cls: 'rag-amber', label: 'Amber' },
  green: { cls: 'rag-green', label: 'Green' },
  critical: { cls: 'rag-red', label: 'Critical' },
  high: { cls: 'rag-red', label: 'High' },
  medium: { cls: 'rag-amber', label: 'Medium' },
  low: { cls: 'rag-green', label: 'Low' },
};

// Strips a leading "[word]" tag if it matches a known severity. Returns { severity, body }.
export function parseLeadingSeverity(text) {
  const m = /^\[(red|amber|green|critical|high|medium|low)\]\s*/i.exec(text);
  if (!m) return { severity: null, body: text };
  return { severity: m[1].toLowerCase(), body: text.slice(m[0].length) };
}

export function SevChip({ severity }) {
  const info = SEVERITY_MAP[severity];
  if (!info) return null;
  return <span className={`sev ${info.cls}`}>{info.label}</span>;
}

// Confidence chip — High is green (good), Medium amber (worth a look), Low red (needs review).
export function ConfidenceChip({ value }) {
  if (!value) return null;
  const key = String(value).toLowerCase();
  const cls = key === 'high' ? 'rag-green' : key === 'medium' ? 'rag-amber' : 'rag-red';
  return <span className={`sev ${cls}`}>{value}</span>;
}

// Parses "Risk [Critical]: rest" or "Issue [High]: rest" and renders the bracket as a chip.
export function renderFindingSummary(summary) {
  const m = /^(Risk|Issue)\s*\[(Critical|High|Medium|Low)\]\s*:\s*(.*)$/i.exec(summary);
  if (!m) return summary;
  const [, kind, sev, body] = m;
  return (
    <>
      <strong>{kind}</strong>{' '}
      <SevChip severity={sev.toLowerCase()} />
      {': '}
      {body}
    </>
  );
}

// Shared footer for narrative / challenge / review — confidence chip + prompt hash + citation.
export function SynthFooter({ item }) {
  return (
    <footer className="synth-footer">
      <ConfidenceChip value={item.confidence} />
      {item.promptVersion ? (
        <span className="synth-meta">prompt <code>{item.promptVersion.slice(0, 14)}…</code></span>
      ) : (
        <span className="synth-meta">template (no LLM)</span>
      )}
      {item.citation?.locator && (
        <span className="synth-meta">cites <code>{item.citation.locator}</code></span>
      )}
    </footer>
  );
}

// Narrative — one paragraph with a leading [red]/[amber]/[green] tag pulled out as a chip.
export function NarrativeArticle({ item }) {
  const { severity, body } = parseLeadingSeverity(item.summary);
  return (
    <article className="synth">
      {severity && <div className="synth-lead"><SevChip severity={severity} /></div>}
      <p className="synth-body">{body}</p>
      <SynthFooter item={item} />
    </article>
  );
}

// Challenge — a series of critiques, each prefixed with [high]/[medium]/[low]. Split on newlines,
// render each as a chip + body row.
export function ChallengeArticle({ item }) {
  const critiques = item.summary
    .split(/\n+/)
    .map(line => line.trim())
    .filter(Boolean)
    .map(line => parseLeadingSeverity(line));
  return (
    <article className="synth">
      <ul className="critique-list">
        {critiques.map((c, i) => (
          <li key={i} className="critique">
            <SevChip severity={c.severity} />
            <span className="critique-body">{c.body}</span>
          </li>
        ))}
      </ul>
      <SynthFooter item={item} />
    </article>
  );
}

// Review — persona-labelled question lists, one line per persona: "Persona: q1 | q2 | q3".
export function ReviewArticle({ item }) {
  const blocks = item.summary
    .split(/\n+/)
    .map(line => line.trim())
    .filter(Boolean)
    .map(line => {
      const m = /^([^:]+):\s*(.*)$/.exec(line);
      if (!m) return { persona: null, questions: [line] };
      return {
        persona: m[1].trim(),
        questions: m[2].split(/\s*\|\s*/).map(q => q.trim()).filter(Boolean),
      };
    });
  return (
    <article className="synth">
      <div className="review-blocks">
        {blocks.map((b, i) => (
          <div key={i} className="review-block">
            {b.persona && <div className="review-persona">{b.persona}</div>}
            <ul className="review-questions">
              {b.questions.map((q, j) => <li key={j}>{q}</li>)}
            </ul>
          </div>
        ))}
      </div>
      <SynthFooter item={item} />
    </article>
  );
}
