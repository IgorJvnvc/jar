import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return (
    <div className="not-found-page">
      <p>8-ball rolled out of bounds.</p>
      <h1>Page not found</h1>
      <Link to="/dashboard" className="button button--primary">
        Back to Dashboard
      </Link>
    </div>
  )
}
