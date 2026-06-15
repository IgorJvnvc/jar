import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import type { FormEvent } from 'react'
import { useAuth } from '../state/use-auth'

type Mode = 'login' | 'register'

export function AuthPage() {
  const navigate = useNavigate()
  const { login, register } = useAuth()

  const [mode, setMode] = useState<Mode>('login')
  const [displayName, setDisplayName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const title = useMemo(
    () => (mode === 'login' ? 'Welcome back to the table' : 'Create your player profile'),
    [mode],
  )

  const submitLabel = mode === 'login' ? 'Break In' : 'Join The Crew'

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setError(null)
    setIsSubmitting(true)

    try {
      if (mode === 'login') {
        await login(email, password)
      } else {
        await register(displayName, email, password)
      }

      navigate('/dashboard', { replace: true })
    } catch {
      setError(mode === 'login' ? 'Invalid email or password.' : 'Could not create account.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="auth-screen">
      <div className="auth-screen__bg" aria-hidden="true" />

      <section className="auth-card">
        <div className="auth-card__header">
          <p>HIGH NOON ACCESS</p>
          <h1>{title}</h1>
          <span>Track sessions, challenge friends, and climb the table leaderboard.</span>
        </div>

        <form onSubmit={handleSubmit} className="auth-card__form">
          {mode === 'register' ? (
            <label>
              Display name
              <input
                value={displayName}
                onChange={(event) => setDisplayName(event.target.value)}
                required
                minLength={2}
                maxLength={60}
                placeholder="Shark Master"
              />
            </label>
          ) : null}

          <label>
            Email
            <input
              type="email"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              required
              placeholder="you@example.com"
            />
          </label>

          <label>
            Password
            <input
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              required
              minLength={8}
              placeholder="At least 8 characters"
            />
          </label>

          {error ? <p className="form-error">{error}</p> : null}

          <button className="button button--primary" type="submit" disabled={isSubmitting}>
            {isSubmitting ? 'Loading...' : submitLabel}
          </button>
        </form>

        <div className="auth-card__footer">
          {mode === 'login' ? (
            <p>
              New in town?{' '}
              <button type="button" onClick={() => setMode('register')}>
                Create account
              </button>
            </p>
          ) : (
            <p>
              Already have an account?{' '}
              <button type="button" onClick={() => setMode('login')}>
                Log in
              </button>
            </p>
          )}
        </div>
      </section>
    </div>
  )
}
