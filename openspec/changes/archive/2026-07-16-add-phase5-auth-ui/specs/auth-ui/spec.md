## ADDED Requirements

### Requirement: Sign-in page presentation and mode toggle

The system SHALL provide a sign-in page (React SPA, route `/login`) that presents both the login and the
register flow on one card, without a separate register route. The card SHALL be a centered surface
(~400px wide) containing a brand header, a two-tab toggle (`Log in` / `Create account`), an email input,
a password input, an error state slot, and a submit button. The submit button label SHALL follow the
selected mode (`Log in` in login mode; `Register & log in` in register mode). A password-rules hint
restating the ASP.NET Identity default rules SHALL appear under the password input **only in register
mode**. Behaviour otherwise matches the existing endpoints — login calls `POST /api/auth/login`, register
calls `POST /api/auth/register` then `POST /api/auth/login`; this requirement does not change either
contract.

#### Scenario: Login mode renders without the rules hint

- **WHEN** the page renders in login mode (default)
- **THEN** the submit button reads `Log in` and the password field has no rules hint

#### Scenario: Register mode shows the rules hint and the register label

- **WHEN** the user selects the `Create account` tab
- **THEN** the submit button label becomes `Register & log in` and the rules hint appears under the
  password field restating the ASP.NET Identity default rules (at least 8 characters, upper + lower +
  digit + symbol)

#### Scenario: Failed sign-in shows a styled error panel

- **WHEN** sign-in fails
- **THEN** the page renders an error panel styled as a red-bordered / red-background surface (not the
  browser default text-colour swap) containing the error message returned by the endpoint

#### Scenario: No forgot-password affordance is rendered

- **WHEN** the page renders in either mode
- **THEN** no "forgot password" link or button is present (forgot-password is not implemented in
  `AuthContext`; admin resets happen out of band)

### Requirement: Post-login redirect target

The sign-in page SHALL navigate the user to `/upload` after a successful sign-in, in both login mode and
register mode.

#### Scenario: Login mode lands on the upload page

- **WHEN** a user completes sign-in in login mode
- **THEN** the application navigates to `/upload`

#### Scenario: Register mode lands on the upload page after auto-login

- **WHEN** a user completes sign-in in register mode (register then auto-login)
- **THEN** the application navigates to `/upload`

### Requirement: Signed-out header shows brand + theme toggle only on `/login`

When the current route is `/login`, the top navigation SHALL hide the tab links (Upload / Portfolio /
Project / History) and the user-menu, leaving only the brand mark and the theme toggle visible. This
requirement SHALL be enforced client-side by the route rather than by mutating the document body's
classes.

#### Scenario: Nav tabs are hidden on the sign-in page

- **WHEN** the current pathname is `/login`
- **THEN** the top navigation shows the brand mark and the theme toggle but does not render the tab
  links or the user-menu — regardless of whether a user object is present in state

#### Scenario: Nav tabs are visible on every other route

- **WHEN** the current pathname is anything other than `/login`
- **THEN** the tab links are rendered (and the user-menu is rendered when authenticated)

### Requirement: Change-password page presentation and success reveal

The system SHALL provide an authenticated change-password page (React SPA, route `/change-password`,
protected by `RequireAuth`) that presents a settings card (~480px wide) with three password fields
(current, new, confirm), a password-rules hint under the "new" field restating the ASP.NET Identity
defaults, a submit action, and a cancel action. On a successful password change the page SHALL reveal a
green success panel restating the fresh-session behaviour of the endpoint: "Password changed. Other
devices have been signed out. This device stays signed in with a fresh session." The success panel SHALL
NOT persist across route changes — leaving the page and returning SHALL show the empty state, not the
success artifact. The page SHALL surface the client-side "new password does not equal confirmation"
check before calling the endpoint.

#### Scenario: Successful change shows the fresh-session success panel

- **WHEN** the user submits a valid current password and a matching new + confirm pair, and the endpoint
  returns success
- **THEN** the page renders the green success panel stating that other devices have been signed out and
  this device stays signed in with a fresh session
- **AND** the three input fields are cleared

#### Scenario: Success panel resets on route change

- **WHEN** the success panel has been shown and the user navigates away and back to `/change-password`
- **THEN** the page renders in its empty state with no success panel visible

#### Scenario: Mismatched confirmation is rejected without an endpoint call

- **WHEN** the user submits with a "new" value that does not equal the "confirm" value
- **THEN** the page shows an error indicating the values do not match and does not call the endpoint

#### Scenario: Endpoint failure shows a styled error

- **WHEN** the endpoint returns a failure (wrong current password, weak new password, etc.)
- **THEN** the page shows a styled error panel with the error message and does not render the success
  panel

### Requirement: Change-password cancel returns to the previous page with a safe fallback

The cancel action on the change-password page SHALL navigate the user back one entry in history when
history exists, and SHALL navigate to `/` as a safe fallback when history does not exist (a fresh tab
opened directly on `/change-password`). Cancel SHALL NOT navigate to a hard-coded route regardless of
where the user arrived from.

#### Scenario: Cancel from a linked entry returns to the previous route

- **WHEN** the user arrives at `/change-password` from the user-menu on `/portfolio` and clicks Cancel
- **THEN** the application navigates back to `/portfolio`

#### Scenario: Cancel with no history falls back to `/`

- **WHEN** the user has opened `/change-password` directly (no history entry) and clicks Cancel
- **THEN** the application navigates to `/`

### Requirement: User menu is a disclosure, not an ARIA menu

The signed-in top navigation SHALL replace the flat list of user info + inline links with a **disclosure**
control: a trigger button (rendering the user's avatar initials, email, and a chevron) that toggles a
popover panel containing a user-info header (display name, email, role chip) and two action items
(`Change password`, `Log out`). The disclosure SHALL NOT use ARIA menu semantics (no `role="menu"`, no
`role="menuitem"`, no roving tabindex, no arrow-key nav): the trigger SHALL declare
`aria-haspopup="true"` and reflect the panel's open state via `aria-expanded`, and the panel SHALL
contain plain `<button>` and `<Link>` elements navigable by Tab. Log out SHALL be presented as a
danger-styled action; Change password SHALL be an anchor-style link. The user's roles as returned by
`GET /api/profile/me` SHALL be rendered verbatim in the header's role chip (joined by ` · `); when
`user.roles` is empty the chip SHALL be omitted rather than rendered as "no roles". The avatar SHALL
present initials derived from `user.userName` (parsing the local part on `.` or `-`, or falling back to
the first two characters), never a network-loaded image.

#### Scenario: Trigger declares disclosure semantics

- **WHEN** the user-menu trigger renders
- **THEN** it is a `<button>` with `aria-haspopup="true"` and an `aria-expanded` attribute that reflects
  the panel state
- **AND** the panel does not carry `role="menu"` and its items do not carry `role="menuitem"`

#### Scenario: Tab moves through items without arrow-key nav

- **WHEN** the panel is open and the user presses Tab from the trigger
- **THEN** focus moves to `Change password`, then to `Log out`; arrow keys do not move focus between
  items

#### Scenario: Role chip renders roles verbatim

- **WHEN** `user.roles` returns `["pmoAdmin", "executive"]`
- **THEN** the header role chip renders `pmoAdmin · executive`

#### Scenario: Role chip is omitted when roles are empty

- **WHEN** `user.roles` returns `[]` or is missing
- **THEN** the header does not render a role chip (no "no roles" placeholder)

#### Scenario: Log out is a danger-styled action, not a link

- **WHEN** the panel renders
- **THEN** the `Log out` item is a `<button>` with a danger visual treatment (red text) that calls the
  existing `logout()` from `AuthContext` on click
- **AND** the `Change password` item is a `<Link>` to `/change-password`

### Requirement: User-menu close rules

The user-menu panel SHALL close on outside click, on Escape, and on route change. Outside-click SHALL be
detected on `mousedown` (not `click`) to avoid a race with re-render. Escape SHALL close the panel and
return focus to the trigger button; outside-click and route-change SHALL NOT force focus.

#### Scenario: Outside click closes the panel

- **WHEN** the panel is open and the user presses the mouse down on any element outside the menu root
- **THEN** the panel closes on `mousedown` (before the ensuing click lands)

#### Scenario: Escape closes and returns focus

- **WHEN** the panel is open and the user presses Escape
- **THEN** the panel closes and focus returns to the trigger button

#### Scenario: Route change closes the panel

- **WHEN** the panel is open and the user clicks `Change password` (which navigates to
  `/change-password`)
- **THEN** the panel closes as the route changes; focus is not forced back to the trigger

### Requirement: Presentation-only boundary — no auth-contract change

This capability SHALL be presentation-only: it SHALL NOT introduce any change to `/api/auth/login`,
`/api/auth/register`, `/api/auth/refresh`, `/api/auth/logout`, `/api/auth/change-password`, the cookie
transport (both cookies remain `httpOnly` + `SameSite=Strict`), the JWT signing, refresh-token rotation,
ASP.NET Identity configuration, the profile surface, or the client-side auth code in `AuthContext.jsx` /
`authFetch` / `RequireAuth`. Existing backend integration tests for the auth flow SHALL stay green
through this change.

#### Scenario: Backend auth tests are unaffected

- **WHEN** the change lands
- **THEN** `AuthEndpointsTests` and every other backend test that covers the `/api/auth/*` surface
  passes without modification

#### Scenario: Client auth wiring is unchanged

- **WHEN** the client-side auth helper files are inspected after the change
- **THEN** `AuthContext.jsx` (including the `login` / `register` / `logout` / `changePassword` methods
  and the `authFetch` refresh cycle) and `RequireAuth` are unchanged in behaviour
