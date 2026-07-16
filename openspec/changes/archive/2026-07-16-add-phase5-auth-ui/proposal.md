## Why

The Phase 5 v2 wireframe (`docs/designs/phase5-wireframe-v2.html`) locks the visual language for the app.
Six of its pages now match — upload, L1 portfolio, L2 project, history — through prior changes. **The three
auth surfaces are the only ones still on bare Pico defaults**, and this leaves the visual system in an
awkward half-migrated state:

- `Login.jsx` is an unstyled `<h1>` + `<form>` with a link-toggle between login and register.
- `ChangePassword.jsx` is an unstyled three-field form; its "signs out other devices" success line does not
  visually distinguish itself from an error line.
- `NavMenu.jsx` renders user info as a bare `<small>` next to two flat `<a>` links — no chip, no dropdown,
  no role indication, no danger-styled log-out.

The gap is visible: signing in drops the user into a wireframe-styled app from stub screens that look like a
different product. Tracks GitHub issue #33.

A second reason to do it now: the Phase 5 tokens on which L1 and L2 currently sit are **only half of the
wireframe's design system**. L1 established `--rag-*` + `.eyebrow` / `.records` / `.sev` on top of Pico's own
`--pico-*` surface variables. The wireframe's actual token base — `--paper` / `--ink` / `--ink-mute` /
`--ink-faint` / `--panel` / `--panel-alt` / `--rule` / `--rule-strong` / `--accent` / `--accent-tint` /
`--sev-*(-bg)`, plus `--font-display` (Georgia) / `--font-ui` (system) / `--font-mono` (ui-monospace) — has
never landed. The auth screens are the first surfaces that require the full palette (paper-on-paper cards,
brass focus, mono field hints, danger colour). Introducing those tokens *only* under `.auth-card` would fork
the design system for a second time; retrofitting L1 and L2 to consume the same tokens in the same change
is the only way to keep "one design system, not two" (from #33).

This is a **presentation-only** change: no change to any `/api/auth/*` endpoint, no change to `AuthContext`,
`authFetch`, cookie/JWT wiring, ASP.NET Identity, or the profile surface. Every backend integration test
(including `AuthEndpointsTests`) stays green.

## What Changes

- **Login page rebuild (`Login.jsx`).** A centered auth-card (~400px) with a brand header, a Log in / Create
  account tab toggle (submit label follows the mode: `Log in` ↔ `Register & log in`), a Register-mode-only
  ASP.NET Identity password-rules hint, and a red-stripe error panel. No forgot-password link (not
  implemented; admin resets happen out of band). Post-login navigation continues to `/upload` (already true).
- **Change-password page rebuild (`ChangePassword.jsx`).** A regular authenticated page shell with a
  480px settings card of three password fields (current, new, confirm), an ASP.NET-rules hint under "new",
  and a green success reveal restating the fresh-session behaviour of the endpoint. Cancel returns to the
  previous page (`navigate(-1)`) with `/` as fallback rather than the hard-coded `/projects`. The success
  reveal resets on unmount (React default; no stale artifact on re-entry).
- **Nav rebuild (`NavMenu.jsx`).** Right-hand flat link list is replaced by an **avatar-chip + email +
  chevron trigger** opening a **disclosure** popover: header with display name / email / role chip, then
  `Change password` (`<Link>`) and a danger-styled `Log out` (`<button>`). Closes on outside-click
  (`mousedown`), Escape (returns focus to trigger), and route change. Nav-tabs remain visible when
  authenticated; on `/login` the tabs and menu are hidden via `useLocation()` — the signed-out header shows
  brand + theme toggle only, matching `body.no-tabs` in the wireframe without touching the body class.
- **Design tokens ported (`styles.scss`).** The wireframe's token base — paper / ink / panel / rule /
  accent / sev / font stacks — lands as CSS custom properties, both themes. Pico is kept underneath for
  form/reset/button primitives (hybrid strategy: least regression risk). New tokens are the only variables
  referenced from new or retrofitted code; `--pico-*` remains only where existing selectors already
  reference it.
- **L1 (`ExecutivePortfolio.jsx`) and L2 (`ProjectFindings.jsx`) restyled to the new tokens.** No JSX
  change beyond token references in class targets — layout, data-path, and behaviour identical. The
  wireframe-conformance requirement already in `executive-portfolio` and `project-status-dashboard` (see the
  existing SHALL "reusing the shared design tokens the Level-1 view established") is satisfied more
  precisely against the actual wireframe palette. `ProjectStatusDashboardDataTests` and
  `ExecutivePortfolioEndpointsTests` stay green.
- **User-menu accessibility is disclosure, not ARIA menu.** The trigger uses `aria-haspopup="true"` +
  `aria-expanded`; the panel is a plain `<div>` of `<button>` / `<Link>` items — no `role="menu"` /
  `role="menuitem"`, no arrow-key nav, no roving tabindex. With two items, Tab is sufficient and screen
  readers get an honest disclosure contract rather than a menu widget promise the UI does not deliver.

Not in scope: any change to `/api/auth/*` contracts, cookies, JWT, refresh-token rotation, ASP.NET Identity
rules, or role-scoped route guards (Phase 6). Password-strength meter, forgot-password, 2FA/SSO/passkeys —
all out of scope, unchanged from #33. `returnTo` after RequireAuth redirects — deferred.

## Capabilities

### New Capabilities

- `auth-ui`: The three authenticated-UI surfaces — the sign-in page (login + register modes on one card),
  the change-password page, and the user-menu disclosure in the top-right of the nav. Owns their
  presentation contract (which UI states exist, when errors and success are shown, when the tabs are
  hidden), their navigation behaviour (post-login redirect, cancel behaviour, close rules for the menu),
  and the presentation-only boundary against the auth backend (no `/api/auth/*`, cookie, or Identity change
  is introduced by this capability).

### Modified Capabilities

None. The L1 (`executive-portfolio`) and L2 (`project-status-dashboard`) specs already require the wireframe
design system; the token-base retrofit is implementation of the existing requirements, not a spec change.
Their behaviour is preserved and their existing tests stay green.

## Impact

- **Code (client only):**
  - `source/AiPMOInsight.Api/ClientApp/src/components/Login.jsx` — rebuilt to the wireframe: tab toggle,
    rules hint (register mode only), red-stripe error panel; behaviour and endpoints unchanged.
  - `source/AiPMOInsight.Api/ClientApp/src/components/ChangePassword.jsx` — rebuilt: settings card, three
    fields with hint, green success reveal, cancel `navigate(-1)` with `/` fallback.
  - `source/AiPMOInsight.Api/ClientApp/src/components/NavMenu.jsx` — user-menu disclosure (chip + email +
    chevron → popover with header + Change password + Log out); nav-tabs hidden on `/login` via
    `useLocation`.
  - `source/AiPMOInsight.Api/ClientApp/src/styles.scss` — wireframe token base (paper / ink / panel / rule /
    accent / sev / font stacks) as CSS custom properties, light + dark; new selectors for `.auth-card /
    .auth-tabs / .auth-tab / .field / .field-label / .field-hint / .auth-error / .auth-submit / .auth-note`,
    `.settings-card / .settings-actions / .success-panel`, `.user-menu / .user-menu-btn / .user-avatar /
    .user-menu-panel / .user-menu-header / .menu-item`; existing L1/L2 selectors updated to consume the new
    tokens.
  - `source/AiPMOInsight.Api/ClientApp/src/components/ExecutivePortfolio.jsx` and `ProjectFindings.jsx` —
    class-target updates only where required by the retrofit; no JSX/data-path change.
- **API / backend:** none.
- **Tests:**
  - `AuthEndpointsTests` (integration): stays green — no contract change.
  - `ProjectStatusDashboardDataTests`, `ExecutivePortfolioEndpointsTests`: stay green — no data-path change.
  - No new JS test — the repo has no JS harness. Coverage = existing backend integration tests + manual
    `/verify` of the six user routes (`/login`, `/change-password`, `/upload`, `/portfolio`, `/projects`,
    `/history`) in both themes (12 screens total).
- **Docs:** a short `CLAUDE.md` note that the auth-ui rebuild + token-base retrofit landed; the roadmap
  Phase 5 auth UI entry flips to ✅.
- **Deferred:** password-strength meter, forgot-password, 2FA/SSO/passkeys, role-scoped route guards
  (Phase 6), `returnTo` handling after `RequireAuth` redirects.
