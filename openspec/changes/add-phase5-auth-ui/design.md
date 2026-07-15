## Context

The client SPA is plain **JSX** (Vite + React Router; `AppRoutes.jsx`), auth via cookie-JWT with
`authFetch` / `RequireAuth` around `AuthContext.jsx`. Styling sits in `styles.scss` — **Pico** as the base
(reset, form primitives, buttons), with a Phase 5 overlay of `--rag-*` CSS custom properties + shared
selectors (`.eyebrow`, `.records`, `.sev`, `.summary-strip`, `.flagged-*`, `.dropzone`, `.pipeline`,
`.l2-header`) that L1 and L2 already consume. Theme switching is via `ThemeContext` / `ThemeToggle`
(explicit toggle plus `prefers-color-scheme` fallback).

The **three auth surfaces are the only pages still on bare Pico defaults**:

- `Login.jsx` — a plain `<h1>` + `<form>` with a link-toggle between login/register mode, no card, no rules
  hint, no styled error state.
- `ChangePassword.jsx` — plain three-field form; the success line is only distinguished from the error
  line by the Pico `--pico-ins-color` / `--pico-del-color` colour swap.
- `NavMenu.jsx` — three flat `<ul>`s inside a Pico `<nav>`; user info is a `<small>`, log-out is a plain
  `<a>` — no chip, no dropdown, no danger emphasis, no role indication.

The auth **contract is already correct** end-to-end: cookies are `httpOnly` + `SameSite=Strict` per
`docs/authentication.md`; `AuthContext.login/register/logout/changePassword` and the four `/api/auth/*`
endpoints work; `changePassword` already revokes every other refresh-token chain and issues a fresh pair
for the calling device. This change touches none of that.

The wireframe's actual design tokens — `--paper`, `--ink`, `--ink-mute`, `--ink-faint`, `--panel`,
`--panel-alt`, `--rule`, `--rule-strong`, `--accent`, `--accent-tint`, `--accent-ink`, `--sev-red/amber/green`
plus `-bg`, `--font-display` (Georgia), `--font-ui` (system), `--font-mono` (ui-monospace), `--shadow-soft` —
have never been ported. L1/L2 currently reference `--pico-*` for surface colours and Pico's font stack for
typography. Introducing the full token base is the second half of this change.

Tracks issue #33.

## Goals / Non-Goals

**Goals:**

- Rebuild the three auth surfaces (Login, Change password, Nav user-menu) to match the wireframe exactly,
  preserving every auth behaviour already tested.
- Port the wireframe's token base to `styles.scss` (light + dark) as the shared design language, and
  retrofit L1/L2 to consume it in the same change so the app reads as one design system on the day this
  ships.
- Ship a user-menu with an accurate accessibility contract — a disclosure, not an ARIA menu — so screen
  readers get honest semantics with the two-item action list.

**Non-Goals:**

- Any change to `/api/auth/*` (login, register, refresh, logout, change-password), the cookie / JWT model,
  refresh-token rotation, ASP.NET Identity rules, or the profile surface.
- Role-scoped route guards (deferred to Phase 6). Roles are displayed but not enforced client-side; server
  authorization remains authoritative.
- Password-strength meter, forgot-password, 2FA / SSO / passkeys — all out of scope, unchanged from #33.
- `returnTo` handling after `RequireAuth` redirects — deep-linked signed-out users still land on `/upload`
  after login, not the originally-requested route. Flagged as follow-on.
- Any new JS test harness — the repo has none and this change does not introduce one.

## Decisions

**1. Presentation-only boundary — auth contract is frozen.** No file under `AiPMOInsight.Application`,
`AiPMOInsight.Infrastructure`, `AuthContext.jsx`, or the `/api/auth/*` endpoint code is touched. Behaviour
that IS in scope: the redirect target of `Login.jsx` (already `/upload`, kept), the cancel target of
`ChangePassword.jsx` (`navigate(-1)` with `/` fallback, replacing the hard-coded `/projects`), and the
route-conditional rendering of the nav (tabs + user-menu hidden on `/login`). *Alternative considered:*
add a `returnTo` query-param round-trip so deep-linked users land where they wanted — rejected as scope
creep for a POC; flagged as follow-on. `AuthEndpointsTests` stays green as the guard.

**2. Token strategy: hybrid — Pico primitives underneath, wireframe tokens overlay on semantic surfaces
(C-iii).** The wireframe token base (`--paper`, `--ink*`, `--panel*`, `--rule*`, `--accent*`, `--sev-*`,
`--font-display / --font-ui / --font-mono`, `--shadow-soft`) lands as CSS custom properties in
`styles.scss`. Pico is kept as the base for `<input>`, `<button>`, reset, and generic form primitives — the
things we would otherwise hand-write. Every new or retrofitted selector references the wireframe tokens
only; `--pico-*` continues to appear only where existing selectors already use it and where behaviour is
correct. *Alternatives considered:* (a) drop Pico entirely and hand-roll the reset from the token set —
rejected as unnecessary work for zero product value; (b) scope wireframe tokens under `.auth-card` /
`.settings-card` / `.user-menu` only — rejected because it would fork the design system for a second time
(the L1/L2 tokens are already Pico-shaped) and force a full retrofit later anyway. The hybrid is the
smallest diff that satisfies "one design system, not two."

**3. Retrofit L1 (`ExecutivePortfolio.jsx`) and L2 (`ProjectFindings.jsx`) in the same change.** The
retrofit is CSS-only: class targets in `styles.scss` that currently reference `--pico-muted-color` /
`--pico-muted-border-color` / `--pico-card-*-background-color` / `--pico-primary` are switched to reference
the wireframe tokens (`--ink-mute` / `--rule` / `--panel` / `--panel-alt` / `--accent`). No JSX changes.
The data path (`Promise.allSettled` fetches, `healthState` mapping, `ExecutivePortfolioEndpoints`, roll-up
math) is untouched. `ProjectStatusDashboardDataTests` and `ExecutivePortfolioEndpointsTests` remain the
regression guards. *Alternative considered:* leave L1/L2 on `--pico-*` and revisit later — rejected: it
would freeze the design system in its half-migrated state, and the token divergence between "L1/L2 on
Pico surface variables, auth on wireframe variables" is exactly the fork the issue's "one design system"
constraint forbids.

**4. Fonts: Georgia display for every `h1` / page-title, system UI for body, `ui-monospace` for
numbers/IDs/hints.** `--font-display: Georgia, 'Times New Roman', serif; --font-ui: -apple-system,
BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; --font-mono: ui-monospace, 'SFMono-Regular', 'Cascadia
Mono', Consolas, monospace;` — all system fonts, no `@font-face`, no network fetch (issue constraint).
Applied consistently across L1 / L2 / History / Upload / auth. `font-variant-numeric: tabular-nums`
anywhere digits align (already used in `.records .conf` / `.l2-header .conf`). *Alternative considered:*
use Georgia only in the auth-card brand line and keep Pico's stack elsewhere — rejected: half-adopting the
serif reads as an accident, not a choice.

**5. Dark theme: ship in the same change.** The repo already toggles `data-theme='dark'` + honours
`prefers-color-scheme`. Every new token has a `:root[data-theme='dark']` override, plus the
`prefers-color-scheme: dark` fallback inside `:root:not([data-theme='light'])`, matching the pattern the
`--rag-*` tokens already established. *Alternative considered:* ship light-only, add dark next iteration —
rejected: creates immediate visible-in-app debt and doubles the risk when the retrofit is later applied.

**6. User-menu is a disclosure, not an ARIA menu.** The trigger is `<button aria-haspopup="true"
aria-expanded={open} aria-controls="user-menu-panel">`. The panel is a `<div id="user-menu-panel">`
containing a header block, a `<Link to="/change-password" class="menu-item">`, and a `<button
class="menu-item danger" onClick={logout}>`. No `role="menu"`, no `role="menuitem"`, no roving tabindex,
no arrow-key nav. Tab moves through the two items naturally; tabbing past the last item closes the panel
(via focus-out listener) and continues to the next focusable element on the page. *Rationale:* an ARIA
menu is a heavyweight widget contract (arrow-key traversal, `aria-activedescendant`, focus trap on close);
two items do not justify that complexity, and marking-up "menu" while behaving as "disclosure" gives
screen-reader users an inaccurate promise. *Alternative considered:* mark up as ARIA menu with roving
tabindex and arrow-key handlers — rejected as over-engineering for the surface and inconsistent with the
issue's AC (which describes a disclosure).

**7. Menu close rules: outside-`mousedown`, Escape, route-change.** `document.mousedown` (not `click`)
avoids a race where the menu closes on `mousedown` outside but the ensuing `click` lands on a re-rendered
element. `keydown` on Escape closes and calls `triggerRef.current?.focus()` to return focus. A
`useEffect` keyed on `location.pathname` closes the panel when the user picks Change password (which
navigates). Focus is only returned to the trigger on Escape — on outside-click the user's focus intent is
elsewhere, and on route-change the new route owns focus.

**8. Nav-tabs hidden on `/login` via `useLocation()`, not a body class.** Inside `NavMenu.jsx`,
`useLocation().pathname === '/login'` conditionally renders the tabs section and the user-menu; the brand
and theme toggle stay. This produces the wireframe's `body.no-tabs` behaviour without a DOM-side effect
that mutates `document.body.classList`, which fights React's declarative model.

**9. Change-password: cancel returns to the previous page, success reveal resets on unmount.** Cancel
calls `navigate(-1)`; if there is no history entry (fresh tab opened directly on `/change-password`), the
fallback is `navigate('/')`. Success reveal is component-local state (`useState`) — leaving the route
unmounts the component, so re-entering renders the empty state naturally with no explicit reset code. This
is the smallest correct implementation of the "no stale artifact on return" AC.

**10. Login page hides nav-tabs via the same rule; sign-out flow lands on `/login`.** `NavMenu` already
uses `useLocation`; the sign-out handler (`logout()` then `navigate('/login')`) is preserved verbatim from
today's `NavMenu` — the user-menu just becomes the trigger.

**11. Roles rendered as-returned from `/api/profile/me`.** The wireframe shows a role chip formatted
`pmoAdmin · executive` — literal `user.roles.join(' · ')` matches. No client-side normalisation, no
sorting. If `user.roles` is empty the chip is omitted (not "no roles").

## Risks / Trade-offs

- **Visual regression on L1/L2 during the retrofit.** The repo has no visual-regression harness and no
  JS/JSX test harness generally (see project memory: "The repo has no JS test harness — the L2 data path
  is locked by the backend integration test"). Mitigation: the retrofit is CSS-only; the backend data-path
  tests (`ProjectStatusDashboardDataTests`, `ExecutivePortfolioEndpointsTests`) stay as the regression
  contract for behaviour, and 12 explicit `/verify` passes (6 routes × 2 themes) cover the visual side. If
  a regression is caught, it is localised to `styles.scss` since JSX is untouched.
- **Two tokens systems visible in `styles.scss` during transition.** The `--pico-*` references remaining
  in already-shipped selectors that were not retrofitted (e.g. Pico's default form padding, header
  spacing) coexist with the new tokens. We accept this as an intended state: retrofitting every last
  `--pico-*` reference in this change would balloon the scope. Rule of thumb enforced in review: **new or
  retrofitted code SHALL NOT reference `--pico-*`** — only the wireframe tokens.
- **Disclosure vs the wireframe's `role="menu"` markup.** The wireframe HTML sets `role="menu"` and
  `role="menuitem"` on the popover; this change deliberately ships without those attributes. Reviewers
  familiar with the wireframe may flag it — the design.md decision (§6) is the record of the deliberate
  divergence and its rationale (honest a11y contract for a two-item action list).
- **Georgia is a system font on macOS, Windows, and most Linux distros — but not on all embedded / Linux-server
  browsers.** Fallback chain `Georgia, 'Times New Roman', serif` handles the missing-font case; the visual
  will differ but remain legible. Given the app is used inside a browser on a work machine (not embedded),
  this is acceptable.
- **No JS test harness means the user-menu accessibility (Escape, outside-click, focus return) is only
  verified by hand.** Acceptable for a POC; a keyboard-testing pass is called out in `tasks.md` (§6.3) so
  it is not skipped.
