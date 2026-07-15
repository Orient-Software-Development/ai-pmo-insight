import { Home } from './components/Home';
import { ExecutivePortfolio } from './components/ExecutivePortfolio';
import { ProjectFindings } from './components/ProjectFindings';
import { History } from './components/History';
import { Login } from './components/Login';
import { ChangePassword } from './components/ChangePassword';
import { RequireAuth } from './AuthContext';

const AppRoutes = [
  {
    index: true,
    element: <Home />,
  },
  {
    path: '/login',
    element: <Login />,
  },
  {
    // Protected: redirects to /login when signed out.
    path: '/portfolio',
    element: (
      <RequireAuth>
        <ExecutivePortfolio />
      </RequireAuth>
    ),
  },
  {
    // Protected: redirects to /login when signed out.
    path: '/projects',
    element: (
      <RequireAuth>
        <ProjectFindings />
      </RequireAuth>
    ),
  },
  {
    // Protected: redirects to /login when signed out.
    path: '/history',
    element: (
      <RequireAuth>
        <History />
      </RequireAuth>
    ),
  },
  {
    // Protected: redirects to /login when signed out.
    path: '/change-password',
    element: (
      <RequireAuth>
        <ChangePassword />
      </RequireAuth>
    ),
  },
];

export default AppRoutes;
