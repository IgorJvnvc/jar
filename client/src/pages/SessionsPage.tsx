import { useEffect, useMemo, useState, type ReactNode } from 'react'
import { hallsApi } from '../api/halls-api'
import { sessionsApi } from '../api/sessions-api'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { useAuthenticatedRequest } from '../hooks/use-authenticated-request'
import { formatDateTime, formatDurationFrom } from '../lib/date'
import type {
  EndSessionRequest,
  GameLogEntry,
  GameType,
  PoolHallResponse,
  PoolHallTableResponse,
  SessionResponse,
  StartSessionRequest,
} from '../lib/types'

const LAST_GAME_TYPE_KEY = 'jar:last-game-type'

const GAME_TYPE_LABELS: Record<GameType, string> = {
  EightBall: '8-Ball',
  NineBall: '9-Ball',
}

function gamesStorageKey(sessionId: string): string {
  return `jar:session-games:${sessionId}`
}

function loadLastGameType(): GameType {
  return localStorage.getItem(LAST_GAME_TYPE_KEY) === 'NineBall' ? 'NineBall' : 'EightBall'
}

function readStoredGames(sessionId: string): GameLogEntry[] {
  const raw = localStorage.getItem(gamesStorageKey(sessionId))
  if (!raw) {
    return []
  }

  try {
    const parsed: unknown = JSON.parse(raw)
    return Array.isArray(parsed) ? (parsed as GameLogEntry[]) : []
  } catch {
    return []
  }
}

function emptyDraft(gameType: GameType): GameLogEntry {
  return {
    gameType,
    brokeThisRack: true,
    breakPots: 0,
    ballsPotted: 0,
    snookersFaced: 0,
    snookersEscaped: 0,
    won: true,
    goldenBreak: false,
  }
}

function isGoldenEligible(entry: GameLogEntry): boolean {
  return (entry.brokeThisRack && entry.won) || (!entry.brokeThisRack && !entry.won)
}

// Mirrors the backend GameLogEntry validation so the UI never builds an invalid rack.
function normalizeDraft(entry: GameLogEntry): GameLogEntry {
  return {
    ...entry,
    breakPots: entry.brokeThisRack ? entry.breakPots : 0,
    snookersEscaped: Math.min(entry.snookersEscaped, entry.snookersFaced),
    goldenBreak: isGoldenEligible(entry) ? entry.goldenBreak : false,
  }
}

function describeGame(game: GameLogEntry): string {
  const parts: string[] = [
    game.brokeThisRack ? `Broke (${game.breakPots} on break)` : 'Opponent broke',
    `${game.ballsPotted} potted`,
  ]

  if (game.snookersFaced > 0) {
    parts.push(`${game.snookersEscaped}/${game.snookersFaced} snookers escaped`)
  }

  if (game.goldenBreak) {
    parts.push(game.won ? 'golden break' : 'lost to golden break')
  }

  return parts.join(' \u2022 ')
}

export function SessionsPage() {
  const authenticatedRequest = useAuthenticatedRequest()

  const [halls, setHalls] = useState<PoolHallResponse[]>([])
  const [activeSession, setActiveSession] = useState<SessionResponse | null>(null)
  const [recentSessions, setRecentSessions] = useState<SessionResponse[]>([])
  const [hallTables, setHallTables] = useState<Record<string, PoolHallTableResponse[]>>({})
  const [selectedHallId, setSelectedHallId] = useState<string>('')
  const [selectedTableId, setSelectedTableId] = useState<string>('')
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
      setFeedback('Session started. Log each rack as you play.')
    } catch {
      setError('Could not start session.')
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleEndSession = async (sessionId: string, payload: EndSessionRequest) => {
    setError(null)
    setFeedback(null)
    setIsSubmitting(true)

    try {
      const completed = await authenticatedRequest((accessToken) =>
        sessionsApi.end(accessToken, sessionId, payload),
      )

      localStorage.removeItem(gamesStorageKey(sessionId))
      setActiveSession(null)
      setRecentSessions((current) => [completed, ...current])

      if (completed.goldenBreaks > 0) {
        setFeedback(
          `Golden break! +${completed.awardedPoints} points and flagged for a quick review.`,
        )
      } else if (completed.isFlaggedForValidation) {
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
        subtitle="Log every rack as you play so your skill stats and points reflect the session."
      />

      {isLoading ? <p className="state-text">Loading sessions...</p> : null}
      {error ? <p className="state-text state-text--error">{error}</p> : null}
      {feedback ? <p className="state-text state-text--success">{feedback}</p> : null}

      {activeSession ? (
        <ActiveSessionPanel
          key={activeSession.id}
          session={activeSession}
          isSubmitting={isSubmitting}
          onSubmit={(payload) => handleEndSession(activeSession.id, payload)}
        />
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
                  <strong>{`${session.ballsPotted} pots \u2022 ${session.gamesWon}-${session.gamesLost}`}</strong>
                  <span>{formatDateTime(session.startedAtUtc)}</span>
                </div>
                <small>
                  {formatDurationFrom(session.startedAtUtc, session.endedAtUtc)}
                  {session.awardedPoints > 0 ? ` \u2022 +${session.awardedPoints} pts` : ''}
                  {session.goldenBreaks > 0 ? ` \u2022 ${session.goldenBreaks}x golden` : ''}
                  {session.isFlaggedForValidation ? ' \u2022 flagged' : ''}
                </small>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </div>
  )
}

type ActiveSessionPanelProps = {
  session: SessionResponse
  isSubmitting: boolean
  onSubmit: (payload: EndSessionRequest) => void
}

function ActiveSessionPanel({ session, isSubmitting, onSubmit }: ActiveSessionPanelProps) {
  const [games, setGames] = useState<GameLogEntry[]>(() => readStoredGames(session.id))
  const [draft, setDraft] = useState<GameLogEntry>(() => emptyDraft(loadLastGameType()))
  const [notes, setNotes] = useState<string>('')
  const [isReviewing, setIsReviewing] = useState(false)

  // Persist the rack log so an accidental reload mid-session does not lose progress.
  useEffect(() => {
    localStorage.setItem(gamesStorageKey(session.id), JSON.stringify(games))
  }, [session.id, games])

  const tally = useMemo(
    () =>
      games.reduce(
        (acc, game) => ({
          racks: acc.racks + 1,
          wins: acc.wins + (game.won ? 1 : 0),
          losses: acc.losses + (game.won ? 0 : 1),
          pots: acc.pots + game.breakPots + game.ballsPotted,
          golden: acc.golden + (game.goldenBreak && game.won ? 1 : 0),
        }),
        { racks: 0, wins: 0, losses: 0, pots: 0, golden: 0 },
      ),
    [games],
  )

  const updateDraft = (patch: Partial<GameLogEntry>) => {
    setDraft((current) => normalizeDraft({ ...current, ...patch }))
  }

  const handleAddGame = () => {
    const entry = normalizeDraft(draft)
    setGames((current) => [...current, entry])
    localStorage.setItem(LAST_GAME_TYPE_KEY, entry.gameType)
    setDraft(emptyDraft(entry.gameType))
  }

  const handleRemoveGame = (index: number) => {
    setGames((current) => current.filter((_, position) => position !== index))
  }

  const handleSubmit = () => {
    onSubmit({ games, notes: notes.trim() ? notes.trim() : null })
  }

  return (
    <Card title="Active Session" subtitle="Log each rack, then stop to review and submit.">
      <div className="active-session-summary">
        <p>
          Started: <strong>{formatDateTime(session.startedAtUtc)}</strong>
        </p>
        <p>
          Elapsed: <strong>{formatDurationFrom(session.startedAtUtc, null)}</strong>
        </p>
      </div>

      <div className="session-tally">
        <TallyStat label="Racks" value={tally.racks} />
        <TallyStat label="Won-Lost" value={`${tally.wins}-${tally.losses}`} />
        <TallyStat label="Pots" value={tally.pots} />
        <TallyStat label="Golden" value={tally.golden} />
      </div>

      {!isReviewing ? (
        <>
          <hr className="section-divider" />
          <div className="game-logger">
            <h3 className="game-logger__title">Log a rack</h3>

            <ToggleField label="Game">
              <Segmented
                options={[
                  { value: 'EightBall', label: GAME_TYPE_LABELS.EightBall },
                  { value: 'NineBall', label: GAME_TYPE_LABELS.NineBall },
                ]}
                value={draft.gameType}
                onChange={(value) => updateDraft({ gameType: value })}
              />
            </ToggleField>

            <ToggleField label="Break">
              <Segmented
                options={[
                  { value: 'me', label: 'I broke' },
                  { value: 'opponent', label: 'Opponent broke' },
                ]}
                value={draft.brokeThisRack ? 'me' : 'opponent'}
                onChange={(value) => updateDraft({ brokeThisRack: value === 'me' })}
              />
            </ToggleField>

            <div className="session-form-grid session-form-grid--cols">
              {draft.brokeThisRack ? (
                <NumberField
                  label="Break pots"
                  min={0}
                  value={draft.breakPots}
                  onChange={(value) => updateDraft({ breakPots: value })}
                />
              ) : null}
              <NumberField
                label="Balls potted (in play)"
                min={0}
                value={draft.ballsPotted}
                onChange={(value) => updateDraft({ ballsPotted: value })}
              />
              <NumberField
                label="Snookers faced"
                min={0}
                value={draft.snookersFaced}
                onChange={(value) => updateDraft({ snookersFaced: value })}
              />
              <NumberField
                label="Snookers escaped"
                min={0}
                max={draft.snookersFaced}
                value={draft.snookersEscaped}
                onChange={(value) => updateDraft({ snookersEscaped: value })}
              />
            </div>

            <ToggleField label="Result">
              <Segmented
                options={[
                  { value: 'won', label: 'Won' },
                  { value: 'lost', label: 'Lost' },
                ]}
                value={draft.won ? 'won' : 'lost'}
                onChange={(value) => updateDraft({ won: value === 'won' })}
              />
            </ToggleField>

            {isGoldenEligible(draft) ? (
              <label className="checkbox-field">
                <input
                  type="checkbox"
                  checked={draft.goldenBreak}
                  onChange={(event) => updateDraft({ goldenBreak: event.target.checked })}
                />
                {draft.brokeThisRack
                  ? 'Won on the break - golden break (+500 pts)'
                  : "Lost to opponent's golden break"}
              </label>
            ) : null}

            <button type="button" className="button button--ghost" onClick={handleAddGame}>
              Add rack
            </button>
          </div>
        </>
      ) : null}

      <hr className="section-divider" />
      <GameLogList games={games} onRemove={handleRemoveGame} />

      <hr className="section-divider" />
      {isReviewing ? (
        <div className="session-review">
          <label className="field-label">
            Notes
            <textarea
              value={notes}
              onChange={(event) => setNotes(event.target.value)}
              rows={3}
              placeholder="How did the session go?"
            />
          </label>
          <div className="button-row">
            <button
              type="button"
              className="button button--ghost"
              onClick={() => setIsReviewing(false)}
              disabled={isSubmitting}
            >
              Back to logging
            </button>
            <button
              type="button"
              className="button button--primary"
              onClick={handleSubmit}
              disabled={isSubmitting}
            >
              {isSubmitting ? 'Submitting...' : 'Submit Report'}
            </button>
          </div>
        </div>
      ) : (
        <button
          type="button"
          className="button button--primary"
          onClick={() => setIsReviewing(true)}
        >
          Stop & Review
        </button>
      )}
    </Card>
  )
}

type SegmentedOption<T extends string> = {
  value: T
  label: string
}

type SegmentedProps<T extends string> = {
  options: SegmentedOption<T>[]
  value: T
  onChange: (value: T) => void
}

function Segmented<T extends string>({ options, value, onChange }: SegmentedProps<T>) {
  return (
    <div className="segmented">
      {options.map((option) => (
        <button
          key={option.value}
          type="button"
          className={
            value === option.value
              ? 'segmented__option segmented__option--active'
              : 'segmented__option'
          }
          onClick={() => onChange(option.value)}
        >
          {option.label}
        </button>
      ))}
    </div>
  )
}

function ToggleField({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="field-label">
      <span>{label}</span>
      {children}
    </div>
  )
}

function TallyStat({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="session-tally__item">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  )
}

function GameLogList({
  games,
  onRemove,
}: {
  games: GameLogEntry[]
  onRemove: (index: number) => void
}) {
  if (games.length === 0) {
    return <p className="state-text">No racks logged yet.</p>
  }

  return (
    <ul className="stack-list">
      {games.map((game, index) => (
        <li key={index} className="row-item">
          <div>
            <strong>
              {`Rack ${index + 1} \u2022 ${GAME_TYPE_LABELS[game.gameType]} \u2022 ${
                game.won ? 'Won' : 'Lost'
              }`}
            </strong>
            <span>{describeGame(game)}</span>
          </div>
          <button type="button" className="button button--ghost" onClick={() => onRemove(index)}>
            Remove
          </button>
        </li>
      ))}
    </ul>
  )
}

type NumberFieldProps = {
  label: string
  value: number
  min: number
  max?: number
  onChange: (value: number) => void
}

function NumberField({ label, value, min, max, onChange }: NumberFieldProps) {
  return (
    <label className="field-label">
      {label}
      <input
        type="number"
        min={min}
        max={max}
        value={value}
        onChange={(event) => {
          const parsed = Number(event.target.value || 0)
          const lowerBounded = Number.isNaN(parsed) ? min : Math.max(min, parsed)
          onChange(max === undefined ? lowerBounded : Math.min(max, lowerBounded))
        }}
      />
    </label>
  )
}
