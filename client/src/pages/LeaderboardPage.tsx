import { useEffect, useMemo, useState } from 'react'
import { motion } from 'framer-motion'
import { playersApi } from '../api/players-api'
import { Card } from '../components/ui/Card'
import { PageHeader } from '../components/ui/PageHeader'
import { AvatarChip } from '../components/ui/AvatarChip'
import { useAuthenticatedRequest } from '../hooks/use-authenticated-request'
import { useAuth } from '../state/use-auth'
import type { LeaderboardEntryResponse } from '../lib/types'

type SortMode = 'winRate' | 'points' | 'games'

const sortOptions: ReadonlyArray<{ mode: SortMode; label: string }> = [
  { mode: 'winRate', label: 'Win Rate' },
  { mode: 'points', label: 'Points' },
  { mode: 'games', label: 'Games' },
]

const rankBadges = ['gold', 'silver', 'bronze'] as const

function sortEntries(entries: LeaderboardEntryResponse[], mode: SortMode): LeaderboardEntryResponse[] {
  const next = [...entries]

  next.sort((left, right) => {
    if (mode === 'points') {
      return right.points - left.points || right.winRate - left.winRate
    }

    if (mode === 'games') {
      return right.totalGamesPlayed - left.totalGamesPlayed || right.winRate - left.winRate
    }

    return right.winRate - left.winRate || right.points - left.points
  })

  return next
}

export function LeaderboardPage() {
  const authenticatedRequest = useAuthenticatedRequest()
  const { user } = useAuth()

  const [entries, setEntries] = useState<LeaderboardEntryResponse[] | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [sortMode, setSortMode] = useState<SortMode>('winRate')

  useEffect(() => {
    let active = true

    const load = async () => {
      setIsLoading(true)
      setError(null)

      try {
        const next = await authenticatedRequest((accessToken) => playersApi.leaderboard(accessToken))

        if (active) {
          setEntries(next)
        }
      } catch {
        if (active) {
          setError('Could not load the leaderboard.')
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

  const rankedEntries = useMemo(
    () => (entries ? sortEntries(entries, sortMode) : []),
    [entries, sortMode],
  )

  return (
    <div className="page-grid">
      <PageHeader
        eyebrow="High Noon Ranks"
        title="Leaderboard"
        subtitle="Crew standings by win rate, points, and games played."
        actions={
          <div className="segmented" role="group" aria-label="Sort leaderboard">
            {sortOptions.map((option) => (
              <button
                key={option.mode}
                type="button"
                className={
                  option.mode === sortMode
                    ? 'segmented__option segmented__option--active'
                    : 'segmented__option'
                }
                onClick={() => setSortMode(option.mode)}
              >
                {option.label}
              </button>
            ))}
          </div>
        }
      />

      {isLoading ? <p className="state-text">Loading leaderboard...</p> : null}
      {error ? <p className="state-text state-text--error">{error}</p> : null}

      {entries && !isLoading ? (
        <Card title="Crew Standings" subtitle={`${rankedEntries.length} players ranked`}>
          {rankedEntries.length === 0 ? (
            <p className="state-text">No players have joined the crew yet.</p>
          ) : (
            <ol className="leaderboard-list">
              {rankedEntries.map((entry, index) => {
                const badge = index < rankBadges.length ? rankBadges[index] : null
                const isCurrentUser = entry.userId === user?.id

                return (
                  <motion.li
                    key={entry.userId}
                    className={
                      isCurrentUser
                        ? 'leaderboard-row leaderboard-row--self'
                        : 'leaderboard-row'
                    }
                    initial={{ opacity: 0, y: 12 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ duration: 0.25, ease: 'easeOut', delay: Math.min(index * 0.04, 0.4) }}
                  >
                    <span
                      className={
                        badge ? `rank-badge rank-badge--${badge}` : 'rank-badge'
                      }
                    >
                      {index + 1}
                    </span>

                    <AvatarChip
                      displayName={entry.displayName}
                      colorHex={entry.avatarColorHex}
                      size="sm"
                    />

                    <div className="leaderboard-row__identity">
                      <strong>
                        {entry.displayName}
                        {isCurrentUser ? <span className="leaderboard-row__you">You</span> : null}
                      </strong>
                      <span>{entry.title ?? `${entry.totalSessions} sessions logged`}</span>
                    </div>

                    <div className="leaderboard-row__stats">
                      <div className="leaderboard-stat leaderboard-stat--primary">
                        <strong>{Math.round(entry.winRate * 100)}%</strong>
                        <span>win rate</span>
                      </div>
                      <div className="leaderboard-stat">
                        <strong>{entry.totalGamesWon}-{entry.totalGamesLost}</strong>
                        <span>W-L</span>
                      </div>
                      <div className="leaderboard-stat">
                        <strong>{entry.points}</strong>
                        <span>points</span>
                      </div>
                    </div>
                  </motion.li>
                )
              })}
            </ol>
          )}
        </Card>
      ) : null}
    </div>
  )
}
