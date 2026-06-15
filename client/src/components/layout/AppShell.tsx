import { AnimatePresence, motion } from 'framer-motion'
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { formatDistanceToNow } from '../../lib/relative-time'
import { useAuth } from '../../state/use-auth'
import { useNotifications } from '../../state/use-notifications'

const navItems = [
  { to: '/dashboard', label: 'Home' },
  { to: '/sessions', label: 'Sessions' },
  { to: '/duels', label: 'Duels' },
  { to: '/halls', label: 'Halls' },
  { to: '/shop', label: 'Shop' },
  { to: '/profile', label: 'Profile' },
] as const

export function AppShell() {
  const location = useLocation()
  const navigate = useNavigate()
  const { user, logout } = useAuth()
  const { isConnected, notifications, clearNotification, pushEnabled } = useNotifications()

  const handleLogout = async () => {
    await logout()
    navigate('/login', { replace: true })
  }

  return (
    <div className="app-shell">
      <header className="app-shell__topbar">
        <div>
          <p className="app-shell__kicker">POOL CREW TRACKER</p>
          <h1 className="app-shell__title">High Noon Lounge</h1>
          <p className={isConnected ? 'app-shell__realtime app-shell__realtime--on' : 'app-shell__realtime'}>
            {isConnected ? 'Live sync on' : 'Live sync reconnecting'}
          </p>
          <p className={pushEnabled ? 'app-shell__realtime app-shell__realtime--on' : 'app-shell__realtime'}>
            {pushEnabled ? 'Push notifications enabled' : 'Push notifications unavailable'}
          </p>
        </div>
        <div className="app-shell__topbar-actions">
          <span className="app-shell__welcome">{user?.displayName}</span>
          <button type="button" className="button button--ghost" onClick={handleLogout}>
            Log out
          </button>
        </div>
      </header>

      <main className="app-shell__main">
        {notifications.length > 0 ? (
          <div className="toast-stack" role="status" aria-live="polite">
            {notifications.map((item) => (
              <motion.article
                key={item.id}
                className="toast-item"
                initial={{ opacity: 0, y: 10 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, y: -8 }}
              >
                <div>
                  <strong>{item.title}</strong>
                  <p>{item.message}</p>
                  <small>{formatDistanceToNow(item.createdAtUtc)}</small>
                </div>
                <button
                  type="button"
                  className="button button--ghost"
                  onClick={() => clearNotification(item.id)}
                >
                  Dismiss
                </button>
              </motion.article>
            ))}
          </div>
        ) : null}

        <AnimatePresence mode="wait">
          <motion.div
            key={location.pathname}
            initial={{ opacity: 0, y: 12 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -8 }}
            transition={{ duration: 0.25, ease: 'easeOut' }}
            className="route-container"
          >
            <Outlet />
          </motion.div>
        </AnimatePresence>
      </main>

      <nav className="bottom-nav" aria-label="Main navigation">
        {navItems.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            className={({ isActive }) =>
              isActive ? 'bottom-nav__item bottom-nav__item--active' : 'bottom-nav__item'
            }
          >
            {item.label}
          </NavLink>
        ))}
      </nav>
    </div>
  )
}
