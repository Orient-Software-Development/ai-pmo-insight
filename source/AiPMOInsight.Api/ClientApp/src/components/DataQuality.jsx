// Level 3 · Data Quality — stub placeholder. Route wired for nav-tab completeness; real backend
// (portfolio-wide DQ enumeration + remediation) tracked as GitHub issue #35.

export function DataQuality() {
  return (
    <main className="container">
      <div className="eyebrow">Level 3 · Data Quality</div>
      <h1 className="page-title">Data Quality</h1>
      <p className="page-lede">
        Portfolio-wide data quality — missing or inconsistent fields, duplicate identities, unmatched
        projects — with remediation actions. Not yet built.
      </p>

      <div className="flagged-panel wide">
        <div className="flagged-note">
          Tracks <strong>GitHub issue #35</strong>. Will reuse the existing{' '}
          <code>DistinctProjectKeysAsync</code> for portfolio enumeration and layer per-field completeness
          checks over the findings store. No backend endpoint exists yet, so this page is a placeholder
          — clicking around it will not fabricate data.
        </div>
      </div>
    </main>
  );
}
