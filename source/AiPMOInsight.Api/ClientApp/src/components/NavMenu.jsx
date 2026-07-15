import { useEffect, useRef, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { ThemeToggle } from './ThemeToggle';
import { useAuth } from '../AuthContext';

// Derive avatar initials from the userName (email). Splits the local part on `.` or `-` and takes the
// first letter of the first two parts; falls back to the first two characters of the local part; falls
// back to `??` when no userName is present.
function avatarInitials(userName) {
  if (!userName) return '??';
  const local = userName.split('@')[0] ?? '';
  if (!local) return '??';
  const parts = local.split(/[.\-]/).filter(Boolean);
  if (parts.length >= 2) return (parts[0][0] + parts[1][0]).toUpperCase();
  const collapsed = parts[0] ?? local;
  return (collapsed.slice(0, 2) || '??').toUpperCase();
}

export function NavMenu() {
  const { isAuthenticated, user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [open, setOpen] = useState(false);
  const rootRef = useRef(null);
  const triggerRef = useRef(null);

  // Close on outside mousedown, Escape (returning focus), and route change.
  useEffect(() => {
    if (!open) return;
    function onMouseDown(e) {
      if (rootRef.current && !rootRef.current.contains(e.target)) {
        setOpen(false);
      }
    }
    function onKeyDown(e) {
      if (e.key === 'Escape') {
        setOpen(false);
        triggerRef.current?.focus();
      }
    }
    document.addEventListener('mousedown', onMouseDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('mousedown', onMouseDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [open]);

  useEffect(() => {
    setOpen(false);
  }, [location.pathname]);

  async function signOut() {
    setOpen(false);
    await logout();
    navigate('/login');
  }

  const onLoginPage = location.pathname === '/login';

  return (
    <header className="container">
      <nav>
        <ul>
          <li>
            <Link to="/">
              <strong>AiPMOInsight</strong>
            </Link>
          </li>
        </ul>

        {!onLoginPage && (
          <ul>
            <li><Link to="/">Home</Link></li>
            <li><Link to="/upload">Upload</Link></li>
            <li><Link to="/portfolio">Portfolio</Link></li>
            <li><Link to="/projects">Project findings</Link></li>
            <li><Link to="/history">History</Link></li>
          </ul>
        )}

        <ul>
          {!onLoginPage && isAuthenticated && (
            <li>
              <div className="user-menu" ref={rootRef}>
                <button
                  ref={triggerRef}
                  type="button"
                  className="user-menu-btn"
                  aria-haspopup="true"
                  aria-expanded={open}
                  aria-controls="user-menu-panel"
                  onClick={() => setOpen(o => !o)}
                >
                  <span className="user-avatar" aria-hidden="true">
                    {avatarInitials(user?.userName)}
                  </span>
                  <span className="user-email">{user?.userName}</span>
                  <span className="chevron" aria-hidden="true">▾</span>
                </button>

                {open && (
                  <div id="user-menu-panel" className="user-menu-panel">
                    <div className="user-menu-header">
                      <div className="u-email">{user?.userName}</div>
                      {user?.roles && user.roles.length > 0 && (
                        <div className="u-role">{user.roles.join(' · ')}</div>
                      )}
                    </div>
                    <Link to="/change-password" className="menu-item">
                      Change password
                    </Link>
                    <button
                      type="button"
                      className="menu-item danger"
                      onClick={signOut}
                    >
                      Log out
                    </button>
                  </div>
                )}
              </div>
            </li>
          )}
          {!isAuthenticated && !onLoginPage && (
            <li><Link to="/login">Log in</Link></li>
          )}
          <li><ThemeToggle /></li>
        </ul>
      </nav>
    </header>
  );
}
