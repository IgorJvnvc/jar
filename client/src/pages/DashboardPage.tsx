import { useEffect, useMemo, useState } from 'react'
import { hallsApi } from '../api/halls-api'
import { playersApi } from '../api/players-api'
import { profileApi } from '../api/profile-api'
import { sessionsApi } from '../api/sessions-api'
import { shopApi } from '../api/shop-api'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { AvatarChip } from '../components/ui/AvatarChip'
import { formatDateTime, formatDurationFrom } from '../lib/date'
import { useAuthenticatedRequest } from '../hooks/use-authenticated-request'
import type {
  ActiveSessionPlayerResponse,
  CueItemResponse,
  PoolHallResponse,
  ProfileResponse,
  SessionResponse,
} from '../lib/types'

type DashboardData = {
  profile: ProfileResponse
  halls: PoolHallResponse[]
  activePlayers: ActiveSessionPlayerResponse[]
  recentSessions: SessionResponse[]
  equippedCue: CueItemResponse | null
}

export function DashboardPage() {
  const authenticatedRequest = useAuthenticatedRequest()

  const [data, setData] = useState<DashboardData | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let active = true

    const load = async () => {
      setIsLoading(true)
      setError(null)

      try {
        const next = await authenticatedRequest(async (accessToken) => {
          const [profile, halls, activePlayers, recentSessions, cues] = await Promise.all([
            profileApi.getMine(accessToken),
            hallsApi.list(accessToken),
            playersApi.activeSessions(accessToken),
            sessionsApi.recent(accessToken),
            shopApi.listCues(accessToken),
          ])

          return {
            profile,
            halls,
            activePlayers,
            recentSessions,
            equippedCue: cues.find((cue) => cue.isEquipped) ?? null,
          }
        })

        if (active) {
          setData(next)
        }
      } catch {
        if (active) {
          setError('Could not load dashboard data.')
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

  const topHalls = useMemo(() => data?.halls.slice(0, 3) ?? [], [data?.halls])

  return (
    <div className="page-grid">
      <PageHeader
        eyebrow="High Noon Hub"
        title="Dashboard"
        subtitle="Live activity, session stats, and hall spotlight at a glance."
      />

      {isLoading ? <p className="state-text">Loading dashboard...</p> : null}
      {error ? <p className="state-text state-text--error">{error}</p> : null}

      {data ? (
        <>
          <div className="stats-row">
            <Card title="Crew Points" subtitle="Spend in the cue shop">
              <div className="metric-value">{data.profile.points}</div>
            </Card>

            <Card title="Core Stats" subtitle="Manual for now, auto-scaling later">
              <div className="stats-list">
                <StatMini label="Power" value={data.profile.power} />
                <StatMini label="Accuracy" value={data.profile.accuracy} />
                <StatMini label="Cue" value={data.profile.cueControl} />
                <StatMini label="Spin" value={data.profile.spin} />
              </div>
            </Card>

            <Card title="Halls In Network" subtitle="User-discovered places">
              <div className="metric-value">{data.halls.length}</div>
            </Card>
          </div>

          <div className="identity-row">
            <Card title="Title" subtitle="Earned distinction">
              <div className="metric-chip">{data.profile.title ?? 'No active title'}</div>
            </Card>
            <Card title="Debt" subtitle="Owed from lost duels">
              <div className="metric-value">{data.profile.debtPoints}</div>
            </Card>
            <Card title="Equipped Cue" subtitle="Active loadout">
              <div className="metric-chip">{data.equippedCue?.name ?? 'Default Cue'}</div>
            </Card>
          </div>

          <div className="two-column">
            <Card title="Players In Session" subtitle="Who's currently on the tables">
              {data.activePlayers.length === 0 ? (
                <p className="state-text">No players currently in session.</p>
              ) : (
                <ul className="stack-list">
                  {data.activePlayers.map((player) => (
                    <li key={player.sessionId} className="row-item">
                      <AvatarChip displayName={player.displayName} colorHex={player.avatarColorHex} size="sm" />
                      <div>
                        <strong>{player.displayName}</strong>
                        <span>
                          {player.poolHallName}
                          {player.poolHallTableLabel ? ` • ${player.poolHallTableLabel}` : ''}
                        </span>
                      </div>
                      <small>{formatDurationFrom(player.startedAtUtc, null)}</small>
                    </li>
                  ))}
                </ul>
              )}
            </Card>

            <Card title="Top Rated Halls" subtitle="Community quality ranking">
              {topHalls.length === 0 ? (
                <p className="state-text">No halls added yet.</p>
              ) : (
                <ul className="stack-list">
                  {topHalls.map((hall) => (
                    <li key={hall.id} className="row-item row-item--hall">
                      <div>
                        <strong>{hall.name}</strong>
                        <span>{hall.address}</span>
                      </div>
                      <small>
                        {hall.overallScore.toFixed(1)} / 10 ({hall.ratingsCount})
                      </small>
                    </li>
                  ))}
                </ul>
              )}
            </Card>
          </div>

          <Card title="Recent Sessions" subtitle="Your latest reports">
            {data.recentSessions.length === 0 ? (
              <p className="state-text">You have no completed sessions yet.</p>
            ) : (
              <ul className="stack-list">
                {data.recentSessions.slice(0, 6).map((session) => (
                  <li key={session.id} className="row-item row-item--session">
                    <div>
                      <strong>
                        {session.isActive
                          ? 'In Progress'
                          : `${session.ballsPotted} pots • ${session.gamesWon}-${session.gamesLost}`}
                      </strong>
                      <span>{formatDateTime(session.startedAtUtc)}</span>
                    </div>
                    <small>
                      {session.isActive
                        ? formatDurationFrom(session.startedAtUtc, null)
                        : `${formatDurationFrom(session.startedAtUtc, session.endedAtUtc)} • +${session.awardedPoints} pts`}
                    </small>
                  </li>
                ))}
              </ul>
            )}
          </Card>
        </>
      ) : null}
    </div>
  )
}

type StatMiniProps = {
  label: string
  value: number
}

function StatMini({ label, value }: StatMiniProps) {
  return (
    <div className="stat-mini">
      <span>{label}</span>
      <strong>{value.toFixed(1)}</strong>
    </div>
  )
}
