import { useEffect, useMemo, useState } from 'react'
import { hallsApi } from '../api/halls-api'
import { sessionsApi } from '../api/sessions-api'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { useAuthenticatedRequest } from '../hooks/use-authenticated-request'
import { formatDateTime, formatDurationFrom } from '../lib/date'
import type {
  EndSessionRequest,
  PoolHallResponse,
  PoolHallTableResponse,
  SessionResponse,
  StartSessionRequest,
} from '../lib/types'

const defaultEndForm: EndSessionRequest = {
  ballsPotted: 0,
  gamesWon: 0,
  gamesLost: 0,
  snookersEscaped: 0,
  notes: null,
}

export function SessionsPage() {
  const authenticatedRequest = useAuthenticatedRequest()

  const [halls, setHalls] = useState<PoolHallResponse[]>([])
  const [activeSession, setActiveSession] = useState<SessionResponse | null>(null)
  const [recentSessions, setRecentSessions] = useState<SessionResponse[]>([])
  const [hallTables, setHallTables] = useState<Record<string, PoolHallTableResponse[]>>({})
  const [selectedHallId, setSelectedHallId] = useState<string>('')
  const [selectedTableId, setSelectedTableId] = useState<string>('')
  const [endForm, setEndForm] = useState<EndSessionRequest>(defaultEndForm)
  const [feedback, setFeedback] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const tableOptions = useMemo(() => {
    if (!selectedHallId) {
      return []
    }

    return hallTables[selectedHallId] ?? []
  }, [hallTables, selectedHallId])

  useEffect(() => {
    let active = true

    const load = async () => {
      setIsLoading(true)
      setError(null)

      try {
        const result = await authenticatedRequest(async (accessToken) => {
          const [nextHalls, nextRecentSessions] = await Promise.all([
            hallsApi.list(accessToken),
            sessionsApi.recent(accessToken),
          ])

          const nextActiveSession = await sessionsApi
            .getActive(accessToken)
            .catch(() => null as SessionResponse | null)

          return {
            halls: nextHalls,
            activeSession: nextActiveSession,
            recentSessions: nextRecentSessions,
          }
        })

        if (!active) {
          return
        }

        setHalls(result.halls)
        setActiveSession(result.activeSession)
        setRecentSessions(result.recentSessions)
        setSelectedHallId((current) => current || result.halls[0]?.id || '')
      } catch {
        if (active) {
          setError('Could not load sessions.')
        }
      } finally {
        if (active) {
          setIsLoading(false)
        }
      }
    }

    void load()

    return () => {
      active = false
    }
  }, [authenticatedRequest])

  useEffect(() => {
    if (!selectedHallId || hallTables[selectedHallId]) {
      return
    }

    void authenticatedRequest(async (accessToken) => {
      const detail = await hallsApi.getById(accessToken, selectedHallId)
      setHallTables((current) => ({
        ...current,
        [selectedHallId]: detail.tables,
      }))
    })
  }, [authenticatedRequest, hallTables, selectedHallId])

  const handleStartSession = async () => {
    if (!selectedHallId) {
      return
    }

    setError(null)
    setFeedback(null)
    setIsSubmitting(true)

    try {
      const payload: StartSessionRequest = {
        poolHallId: selectedHallId,
        poolHallTableId: selectedTableId || null,
      }

      const nextSession = await authenticatedRequest((accessToken) =>
        sessionsApi.start(accessToken, payload),
      )

      setActiveSession(nextSession)
      setFeedback('Session started. You are now marked as In Session.')
    } catch {
      setError('Could not start session.')
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleEndSession = async () => {
    if (!activeSession) {
      return
    }

    setError(null)
    setFeedback(null)
    setIsSubmitting(true)

    try {
      const completed = await authenticatedRequest((accessToken) =>
        sessionsApi.end(accessToken, activeSession.id, endForm),
      )

      setActiveSession(null)
      setRecentSessions((current) => [completed, ...current])
      setEndForm(defaultEndForm)

      if (completed.isFlaggedForValidation) {
        setFeedback('Session submitted and flagged for review due to pace vs duration.')
      } else {
        setFeedback(`Session submitted. +${completed.awardedPoints} points rewarded.`)
      }
    } catch {
      setError('Could not end session.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="page-grid">
      <PageHeader
        eyebrow="Session Tracker"
        title="Play Sessions"
        subtitle="Start session at a hall, finish with report, and collect points based on play time."
      />

      {isLoading ? <p className="state-text">Loading sessions...</p> : null}
      {error ? <p className="state-text state-text--error">{error}</p> : null}
      {feedback ? <p className="state-text state-text--success">{feedback}</p> : null}

      {activeSession ? (
        <Card title="Active Session" subtitle="Stop session and submit report to score points.">
          <div className="active-session-summary">
            <p>
              Started: <strong>{formatDateTime(activeSession.startedAtUtc)}</strong>
            </p>
            <p>
              Elapsed: <strong>{formatDurationFrom(activeSession.startedAtUtc, null)}</strong>
            </p>
          </div>

          <div className="session-form-grid">
            <NumberField
              label="Balls potted"
              min={0}
              value={endForm.ballsPotted}
              onChange={(value) => setEndForm((current) => ({ ...current, ballsPotted: value }))}
            />
            <NumberField
              label="Games won"
              min={0}
              value={endForm.gamesWon}
              onChange={(value) => setEndForm((current) => ({ ...current, gamesWon: value }))}
            />
            <NumberField
              label="Games lost"
              min={0}
              value={endForm.gamesLost}
              onChange={(value) => setEndForm((current) => ({ ...current, gamesLost: value }))}
            />
            <NumberField
              label="Snookers escaped"
              min={0}
              value={endForm.snookersEscaped}
              onChange={(value) =>
                setEndForm((current) => ({ ...current, snookersEscaped: value }))
              }
            />
          </div>

          <label className="field-label">
            Notes
            <textarea
              value={endForm.notes ?? ''}
              onChange={(event) =>
                setEndForm((current) => ({
                  ...current,
                  notes: event.target.value.trim() ? event.target.value : null,
                }))
              }
              rows={3}
              placeholder="How did the session go?"
            />
          </label>

          <button
            type="button"
            className="button button--primary"
            onClick={handleEndSession}
            disabled={isSubmitting}
          >
            {isSubmitting ? 'Submitting...' : 'End Session'}
          </button>
        </Card>
      ) : (
        <Card title="Start New Session" subtitle="Choose hall and optional table before play starts.">
          <div className="session-form-grid">
            <label className="field-label">
              Hall
              <select
                value={selectedHallId}
                onChange={(event) => {
                  setSelectedHallId(event.target.value)
                  setSelectedTableId('')
                }}
              >
                <option value="">Select hall</option>
                {halls.map((hall) => (
                  <option key={hall.id} value={hall.id}>
                    {hall.name}
                  </option>
                ))}
              </select>
            </label>

            <label className="field-label">
              Table
              <select
                value={selectedTableId}
                onChange={(event) => setSelectedTableId(event.target.value)}
                disabled={!selectedHallId}
              >
                <option value="">Any table</option>
                {tableOptions.map((table) => (
                  <option key={table.id} value={table.id}>
                    {table.tableLabel}
                  </option>
                ))}
              </select>
            </label>
          </div>

          <button
            type="button"
            className="button button--primary"
            onClick={handleStartSession}
            disabled={!selectedHallId || isSubmitting}
          >
            {isSubmitting ? 'Starting...' : 'Start Session'}
          </button>
        </Card>
      )}

      <Card title="Recent Sessions" subtitle="Latest submitted reports">
        {recentSessions.length === 0 ? (
          <p className="state-text">No sessions yet.</p>
        ) : (
          <ul className="stack-list">
            {recentSessions.map((session) => (
              <li key={session.id} className="row-item row-item--session">
                <div>
                  <strong>{`${session.ballsPotted} pots • ${session.gamesWon}-${session.gamesLost}`}</strong>
                  <span>{formatDateTime(session.startedAtUtc)}</span>
                </div>
                <small>
                  {formatDurationFrom(session.startedAtUtc, session.endedAtUtc)}
                  {session.awardedPoints > 0 ? ` • +${session.awardedPoints} pts` : ''}
                  {session.isFlaggedForValidation ? ' • flagged' : ''}
                </small>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </div>
  )
}

type NumberFieldProps = {
  label: string
  value: number
  min: number
  onChange: (value: number) => void
}

function NumberField({ label, value, min, onChange }: NumberFieldProps) {
  return (
    <label className="field-label">
      {label}
      <input
        type="number"
        min={min}
        value={value}
        onChange={(event) => onChange(Number(event.target.value || 0))}
      />
    </label>
  )
}
