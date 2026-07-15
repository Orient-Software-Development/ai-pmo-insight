import { useEffect, useRef, useState } from 'react';
import { Link, NavLink, useLocation, useNavigate } from 'react-router-dom';
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

// Prefix-match so /portfolio/anything keeps the tab active. Root and auth surfaces highlight nothing.
function isTabActive(pathname, tab) {
  if (tab === '/upload') return pathname === '/upload' || pathname.startsWith('/upload/');
  if (tab === '/portfolio') return pathname === '/portfolio' || pathname.startsWith('/portfolio/');
  if (tab === '/projects') return pathname === '/projects' || pathname.startsWith('/projects/');
  if (tab === '/data-quality') return pathname === '/data-quality' || pathname.startsWith('/data-quality/');
  if (tab === '/history') return pathname === '/history' || pathname.startsWith('/history/');
  return false;
}

function UploadIcon() {
  return (
    <svg width="12" height="12" viewBox="0 0 12 12" fill="none" aria-hidden="true">
      <path
        d="M6 1.5v6.5M6 1.5L3.5 4M6 1.5L8.5 4M2.5 10h7"
        stroke="currentColor"
        strokeWidth="1.4"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

export function NavMenu() {
  const { isAuthenticated, user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const [open, setOpen] = useState(false);
  const rootRef = useRef(null);
  const triggerRef = useRef(null);

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
  const pathname = location.pathname;

  const tabClass = to => `nav-tab${isTabActive(pathname, to) ? ' active' : ''}`;

  return (
    <header className="app-nav">
      <div className="nav-inner">
        <Link to="/" className="nav-brand">
          <span className="nav-brand-mark" aria-hidden="true">A</span>
          <span className="nav-brand-name">AI PMO Insight</span>
        </Link>

        {!onLoginPage && (
          <nav className="nav-tabs" role="tablist" aria-label="Dashboard sections">
            <NavLink to="/upload" className={tabClass('/upload')} role="tab" aria-selected={isTabActive(pathname, '/upload')}>
              <UploadIcon /> Upload
            </NavLink>
            <NavLink to="/portfolio" className={tabClass('/portfolio')} role="tab" aria-selected={isTabActive(pathname, '/portfolio')}>
              <span className="num">01</span> Portfolio
            </NavLink>
            <NavLink to="/projects" className={tabClass('/projects')} role="tab" aria-selected={isTabActive(pathname, '/projects')}>
              <span className="num">02</span> Project
            </NavLink>
            <NavLink to="/data-quality" className={tabClass('/data-quality')} role="tab" aria-selected={isTabActive(pathname, '/data-quality')}>
              <span className="num">03</span> Data Quality
            </NavLink>
            <NavLink to="/history" className={tabClass('/history')} role="tab" aria-selected={isTabActive(pathname, '/history')}>
              History
            </NavLink>
          </nav>
        )}

        <div className="nav-meta">
          {!onLoginPage && isAuthenticated && (
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
                  <button type="button" className="menu-item danger" onClick={signOut}>
                    Log out
                  </button>
                </div>
              )}
            </div>
          )}
          {!isAuthenticated && !onLoginPage && (
            <Link to="/login" className="nav-tab">Log in</Link>
          )}
          <ThemeToggle />
        </div>
      </div>
    </header>
  );
}
