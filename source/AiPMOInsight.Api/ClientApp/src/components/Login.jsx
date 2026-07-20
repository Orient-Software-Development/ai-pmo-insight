import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../AuthContext';

export function Login() {
  const { login, register } = useAuth();
  const navigate = useNavigate();

  const [mode, setMode] = useState('login'); // 'login' | 'register'
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);

  function switchMode(next) {
    setMode(next);
    setError(null);
  }

  async function submit(e) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      if (mode === 'register') {
        await register(email, password);
      }
      await login(email, password);
      navigate('/upload'); // Phase 5 cold-start flow lands on the ingest page (see #33 / #38).
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="auth-page">
      <div>
        <div className="auth-card">
          <div className="auth-header">
            <div className="brand-mark" aria-hidden="true">A</div>
            <div>
              <div className="brand-name">AI PMO Insight</div>
              <div className="brand-sub">Sign in to continue</div>
            </div>
          </div>

          <div className="auth-tabs" role="tablist" aria-label="Auth mode">
            <button
              type="button"
              role="tab"
              aria-selected={mode === 'login'}
              className={`auth-tab ${mode === 'login' ? 'active' : ''}`}
              onClick={() => switchMode('login')}
            >
              Log in
            </button>
            <button
              type="button"
              role="tab"
              aria-selected={mode === 'register'}
              className={`auth-tab ${mode === 'register' ? 'active' : ''}`}
              onClick={() => switchMode('register')}
            >
              Create account
            </button>
          </div>

          <form onSubmit={submit}>
            <label className="field">
              <span className="field-label">Email</span>
              <input
                type="email"
                value={email}
                autoComplete="username"
                placeholder="you@company.com"
                onChange={e => setEmail(e.target.value)}
                required
              />
            </label>

            <label className="field">
              <span className="field-label">Password</span>
              <input
                type="password"
                value={password}
                autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
                onChange={e => setPassword(e.target.value)}
                required
              />
              <div className="field-hint" aria-hidden={mode !== 'register'}>
                At least 8 characters · upper + lower + digit + symbol (ASP.NET Identity default).
              </div>
            </label>

            {error && (
              <div className="auth-error" role="alert">
                <b>Sign-in failed.</b> {error}
              </div>
            )}

            <button
              type="submit"
              className="auth-submit"
              aria-busy={busy}
              disabled={busy}
            >
              {mode === 'login' ? 'Log in' : 'Register & log in'}
            </button>
          </form>
        </div>

        <div className="auth-note">
          Cookie-transported JWT · <code>httpOnly</code> · <code>SameSite=Strict</code>
        </div>
      </div>
    </main>
  );
}
