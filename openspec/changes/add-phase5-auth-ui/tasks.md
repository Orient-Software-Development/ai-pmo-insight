> Implement test-first where a test surface exists (red → green → refactor); keep the suite green before
> checking off a task. Scope reminder: **presentation-only, client-side.** No change to `/api/auth/*`,
> `AuthContext.jsx`, `authFetch`, ASP.NET Identity, cookies, or the profile surface. Behaviour that IS
> in scope: post-login redirect target, cancel target on change-password, nav-tabs visibility on `/login`,
> user-menu close rules. The regression contract is `AuthEndpointsTests` +
> `ProjectStatusDashboardDataTests` + `ExecutivePortfolioEndpointsTests` staying green.

## 1. Confirm contracts and harness (no code)

- [x] 1.1 Re-read `AuthContext.jsx` — confirmed 5 methods (`login`, `register`, `logout`, `changePassword`,
      `refresh` cycle behind `authFetch`) + `RequireAuth` guard consumed as-is. No change here.
- [x] 1.2 Re-read `#page-login`, `#page-change-password`, and user-menu sections of
      `docs/designs/phase5-wireframe-v2.html`. Tokens captured: `--paper #F7F6F3 / --panel #FFFFFF /
      --panel-alt #FBFAF6 / --ink #17181C / --ink-mute #55575E / --ink-faint #8A8B92 / --rule #E3E1DA /
      --rule-strong #C8C5BB / --accent #3A5F8A / --accent-ink #23405F / --accent-tint rgba(58,95,138,0.08) /
      --sev-red #B0342C / --sev-amber #C08A2E / --sev-green #2E7D5B` + `-bg` variants + shadow-soft + 3 font
      stacks. Dark palette provided for all colour tokens.
- [x] 1.3 Harness check: `ClientApp` has no JS/JSX test harness. Coverage = `AuthEndpointsTests` +
      `ProjectStatusDashboardDataTests` + `ExecutivePortfolioEndpointsTests` staying green + manual
      `/verify` in §8.
- [x] 1.4 Wireframe v2 carries explicit dark values for every new token (lines 31-50 of the wireframe).
      No derivation needed.

## 2. Lock the auth backend regression guard (TDD)

- [x] 2.1 (red/green) Ran `AuthEndpointsTests` — **14 passed, 0 failed**. Baseline the rebuild must preserve.
- [x] 2.2 Assertion surface noted: login sets cookies + returns 200; register creates user; refresh rotates
      pair; logout revokes + clears cookies; change-password revokes-others + fresh cookie pair; unauth
      flows return 401; profile returns caller's userName + roles.

## 3. Wireframe token base (foundation for everything else)

- [x] 3.1 Added wireframe token base to `styles.scss` on `:root` (light values from wireframe).
- [x] 3.2 Mirrored under `:root[data-theme='dark']` and `@media (prefers-color-scheme: dark)
      { :root:not([data-theme='light']) }` via a shared `@mixin wireframe-dark`. All values taken from the
      wireframe — no derivation needed.
- [x] 3.3 Added a design-system comment header at the top of `styles.scss` capturing the "new/retrofitted
      code MUST NOT reference `--pico-*`" rule.

## 4. Login page rebuild (`Login.jsx` + styles)

- [x] 4.1 Rebuilt `Login.jsx`: `<main className="auth-page">` → `.auth-card` with `.auth-header` (brand
      mark + name + sub), `.auth-tabs` with two `<button role="tab">` toggles, `.field` rows, submit
      label follows mode. `.field-hint` visible only in register mode.
- [x] 4.2 Error state uses `.auth-error` (red-stripe panel from `--sev-red` + `--sev-red-bg`);
      `role="alert"` for screen readers.
- [x] 4.3 Endpoint calls kept verbatim (`login`, `register` from `AuthContext`); `navigate('/upload')`
      preserved; `aria-busy` on submit; `autoComplete` follows mode.
- [x] 4.4 Added `.auth-page / .auth-card / .auth-header / .brand-mark / .brand-name / .brand-sub /
      .auth-tabs / .auth-tab / .field / .field-label / .field-hint / .auth-error / .auth-submit /
      .auth-note` to `styles.scss` — wireframe tokens only.
- [x] 4.5 Removed the "No account? Register" link toggle — tab toggle is now canonical.

## 5. Change-password page rebuild (`ChangePassword.jsx` + styles)

- [x] 5.1 Rebuilt `ChangePassword.jsx`: `<main className="container">` with `.eyebrow`, `h1.page-title`,
      `.page-lede`, and a `.settings-card` containing three `.field` rows and a `.field-hint` under "new".
- [x] 5.2 Success reveal is `.success-panel` (green-stripe) with the fresh-session wording. State is
      `useState`; leaving the route unmounts and resets it (React default — no explicit reset code).
- [x] 5.3 Cancel: `if (window.history.length > 1) navigate(-1); else navigate('/')` — replaced hard-coded
      `/projects`.
- [x] 5.4 Added `.settings-card / .settings-actions / .success-panel / .success-icon / .success-title /
      .link-inline / .page-lede / h1.page-title` selectors.
- [x] 5.5 `changePassword(currentPassword, newPassword)` verbatim; client-side "new ≠ confirm" check
      preserved.

## 6. Nav rebuild (`NavMenu.jsx` + styles)

- [x] 6.1 Replaced right-hand `<ul>` with `.user-menu` block: `<button aria-haspopup="true"
      aria-expanded={open} aria-controls="user-menu-panel">` + avatar + email + chevron. Panel is a
      `<div id="user-menu-panel">` with header (email, role chip) + `<Link>` Change password +
      `<button class="menu-item danger">` Log out.
- [x] 6.2 Close rules via `useEffect`: `document.mousedown` outside-check, `keydown` Escape with
      `triggerRef.current?.focus()`, and a `location.pathname`-keyed effect for route-change close.
- [ ] 6.3 Keyboard pass — **manual verify pending** (needs running app; called out in §8.3).
- [x] 6.4 `useLocation().pathname === '/login'` gates the tabs section and user-menu; brand + theme
      toggle always render.
- [x] 6.5 Added `.user-menu / .user-menu-btn / .user-avatar / .user-menu-panel / .user-menu-header
      (.u-name / .u-email / .u-role) / .menu-item / .menu-item.danger` selectors; danger uses `--sev-red`.
- [x] 6.6 `avatarInitials()` helper: splits local part on `.`/`-`, takes first letter of first two parts;
      falls back to first two chars of local, then `??`.

## 7. L1 and L2 retrofit (styling only, data path frozen)

- [x] 7.1 Replaced all `--pico-muted-color` / `--pico-muted-border-color` /
      `--pico-card-{sectioning-,}background-color` / `--pico-primary` / `--pico-border-radius` /
      `--pico-font-family-monospace` references inside L1/L2 selectors (`.rag-banner`, `.rag-chip`,
      `.rag-review`, `.l2-gap-note`, `.eyebrow`, `.summary-*`, `.rag-bar`, `.rag-legend`, `.sec-head`,
      `.records`, `.flagged-*`, `.dropzone`, `.pipeline`, `.l2-header`) with wireframe tokens
      (`--ink-mute` / `--rule` / `--panel-alt` / `--panel` / `--accent` / `--font-mono` / literal 3px).
- [x] 7.2 `h1.page-title` uses `--font-display`; `.sec-head .sec-title` uses `--font-display`;
      `.records .cite` and `.pipeline-step` use `--font-mono`; added `font-variant-numeric: tabular-nums`
      to `.rag-score` and `.summary-value`.
- [x] 7.3 `ProjectStatusDashboardDataTests` + `ExecutivePortfolioEndpointsTests` (combined run):
      **5 passed, 0 failed**.
- [x] 7.4 See 7.3 — same combined run.
- [x] 7.5 No JSX changes to `ExecutivePortfolio.jsx` or `ProjectFindings.jsx` — retrofit was CSS-only.

## 8. Verify

- [ ] 8.1 **Manual verify pending** — needs the user to run `npm run dev` + `dotnet run` + Docker
      Postgres and drive the full flow (sign in → user menu → change password → success reveal →
      route-away-and-back reset → log out → nav tabs hidden on `/login` → Escape closes menu → outside
      click closes menu).
- [ ] 8.2 **Manual verify pending** — 6 routes × 2 themes = 12 screens visual pass.
- [ ] 8.3 **Manual verify pending** — keyboard-only walk + screen-reader smoke test of user-menu.
- [x] 8.4 Full backend suite: **227 passed** (108 Application + 119 Api, 0 failed). `openspec validate
      --strict` on `add-phase5-auth-ui`: **valid**. Vite production build: **clean, 4.11s** (99KB CSS,
      270KB JS gzipped).
- [x] 8.5 Diff review: touched files are `Login.jsx`, `ChangePassword.jsx`, `NavMenu.jsx`, `styles.scss`,
      and the openspec change dir. **No file touched under `AiPMOInsight.Application`,
      `AiPMOInsight.Infrastructure`, `AiPMOInsight.Domain`, or any API endpoint code.** `AuthContext.jsx`,
      `authFetch`, `RequireAuth`, and every `/api/auth/*` fetch call unchanged.

## 9. Document

- [x] 9.1 Roadmap Phase 5 gains a ✅ `Auth UI rebuild + token-base retrofit` entry
      (`docs/roadmap.md`).
- [x] 9.2 Added a "Auth UI rebuild + token-base retrofit (Phase 5, `add-phase5-auth-ui`, #33)" block to
      `CLAUDE.md` — three surfaces rebuilt, tokens ported, L1/L2 retrofitted, disclosure user-menu (not
      ARIA menu), hybrid Pico + wireframe tokens strategy, all backend tests green.
