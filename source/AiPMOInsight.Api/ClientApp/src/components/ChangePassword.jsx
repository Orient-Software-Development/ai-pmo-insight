import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../AuthContext';

export function ChangePassword() {
  const { changePassword } = useAuth();
  const navigate = useNavigate();

  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState(null);
  const [done, setDone] = useState(false);
  const [busy, setBusy] = useState(false);

  async function submit(e) {
    e.preventDefault();
    setError(null);
    if (newPassword !== confirm) {
      setError('New password and confirmation do not match.');
      return;
    }
    setBusy(true);
    try {
      await changePassword(currentPassword, newPassword);
      setDone(true);
      setCurrentPassword('');
      setNewPassword('');
      setConfirm('');
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  }

  function cancel() {
    // navigate(-1) falls back to the app root when there is no history entry (fresh tab).
    if (window.history.length > 1) {
      navigate(-1);
    } else {
      navigate('/');
    }
  }

  return (
    <main className="container">
      <div className="eyebrow">Account · security</div>
      <h1 className="page-title">Change password</h1>
      <p className="page-lede">
        Changing your password signs you out on every other device — this one stays signed in with a
        fresh session. Every open refresh-token chain for your account is revoked.
      </p>

      <div className="settings-card">
        <form onSubmit={submit}>
          <label className="field">
            <span className="field-label">Current password</span>
            <input
              type="password"
              value={currentPassword}
              autoComplete="current-password"
              onChange={e => setCurrentPassword(e.target.value)}
              required
            />
          </label>

          <label className="field">
            <span className="field-label">New password</span>
            <input
              type="password"
              value={newPassword}
              autoComplete="new-password"
              onChange={e => setNewPassword(e.target.value)}
              required
            />
            <div className="field-hint">
              Minimum 8 characters, mixing upper + lower + digit + symbol (ASP.NET Identity default).
            </div>
          </label>

          <label className="field">
            <span className="field-label">Confirm new password</span>
            <input
              type="password"
              value={confirm}
              autoComplete="new-password"
              onChange={e => setConfirm(e.target.value)}
              required
            />
          </label>

          {error && (
            <div className="auth-error" role="alert">
              <b>Change failed.</b> {error}
            </div>
          )}

          <div className="settings-actions">
            <button type="submit" aria-busy={busy} disabled={busy}>
              Change password
            </button>
            <button type="button" className="link-inline" onClick={cancel}>
              Cancel
            </button>
          </div>
        </form>
      </div>

      {done && (
        <div className="success-panel" role="status">
          <div className="success-icon" aria-hidden="true">✓</div>
          <div>
            <div className="success-title">Password changed</div>
            <p>
              Other devices have been signed out. This device stays signed in with a fresh session.
            </p>
          </div>
        </div>
      )}
    </main>
  );
}
